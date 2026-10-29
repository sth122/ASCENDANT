using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EnemyBTController를 상속받아 고유 기믹(Alert 수색, Roar 및 무리 호출을 추가한 전용 컨트롤러
/// </summary>
public class CreatureBT : EnemyBTController
{
    [Header("Creature Gimmick Configurations")]
    [SerializeField] private float _callAlliesRadius = 15.0f;
    [SerializeField] private LayerMask _allyLayer;

    private readonly Collider[] _allyBuffer = new Collider[16];

    private bool _isAlerted = false;
    private bool _hasSpottedAndRoared = false;
    private Vector3 _heardNoisePosition;

    protected override void SetupComponents()
    {
        _unitId = UnitId.NightmareCreature;
        base.SetupComponents();
    }

    protected override INode BuildBehaviorTree()
    {
        var root = new SelectorNode();

        // 1. 사망 판정 노드
        var deathSeq = new SequenceNode();
        deathSeq.Add(new PredicateNode<CreatureBT>(this, static (c) => c.IsDead));
        deathSeq.Add(new ActionNode(ExecuteDeath));
        root.Add(deathSeq);

        // 2. 그로기(Stun) 브랜치
        var groggySeq = new SequenceNode();
        groggySeq.Add(new PredicateNode<CreatureBT>(this, static (c) =>
            c._enemyStatus != null && c._enemyStatus.IsPoiseBroken && !c._isGroggy));
        groggySeq.Add(new AsyncActionNode(ExecuteGroggyAsync, GetActionToken));
        root.Add(groggySeq);

        // 3. 비동기 액션 진행 중 락
        root.Add(new PredicateNode<CreatureBT>(this, static (c) => c._isActionExecuting));

        // 4. 복귀 분기 (리쉬 초과 시)
        var returnSeq = new SequenceNode();
        returnSeq.Add(new PredicateNode<CreatureBT>(this, static (c) => c.IsExceedingLeashDistance()));
        returnSeq.Add(new ActionNode(TickReturnToSpawn));
        root.Add(returnSeq);

        // 5. 전투 브랜치 (추격 및 공격)
        var combatBranch = new SequenceNode();
        combatBranch.Add(new PredicateNode<CreatureBT>(this, static (c) => c._currentTarget != null));
        combatBranch.Add(BuildCombatSelector());
        root.Add(combatBranch);

        // 6. 발소리 감지 수색 (Alert) 브랜치
        var alertSeq = new SequenceNode();
        alertSeq.Add(new PredicateNode<CreatureBT>(this, static (c) => c._isAlerted));
        alertSeq.Add(new AsyncActionNode(ExecuteAlertSequenceAsync, GetActionToken));
        root.Add(alertSeq);

        // 7. 순찰 브랜치
        root.Add(new ActionNode(TickPatrol));

        return root;
    }

    #region 풀링 소환 전용 시퀀스
    /// <summary>
    /// 풀에서 소환될 때: 동료 부르기 없이 단독 Roar 연출 2초 재생 후 타깃 포착 및 전투 개시
    /// </summary>
    protected override async UniTaskVoid ExecuteSpawnSequenceAsync(CancellationToken token)
    {
        _isActionExecuting = true;
        _movement.StopMovement();

        try
        {
            // 동료 브로드캐스트 없이 단독 표효 모션만 재생
            await PlayStateAndWaitAsync(AnimStateType.Roar, token, force: true);
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            _isActionExecuting = false;
        }

        // 표효 완료 후 즉시 타깃 탐색 및 전투 돌입
        AcquireNearestTarget();
    }
    #endregion

    #region 크리처 전용 AI 액션
    private async UniTask<bool> ExecuteAlertSequenceAsync(CancellationToken ct)
    {
        _isActionExecuting = true;
        _movement.StopMovement();

        try
        {
            Vector3 toNoise = _heardNoisePosition - transform.position;
            toNoise.y = 0f;
            if (toNoise.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(toNoise.normalized);
            }

            // 포복 -> 서기 전환
            await PlayStateAndWaitAsync(AnimStateType.CrawlToState, ct, force: true);

            // 서서 수색 대기
            await PlayStateAndWaitAsync(AnimStateType.BattleIdle, ct, force: true);

            // 서기 -> 포복 복귀
            await PlayStateAndWaitAsync(AnimStateType.StateToCrawl, ct, force: true);
        }
        finally
        {
            _isAlerted = false;
            _isActionExecuting = false;
        }

        return true;
    }
    #endregion

    #region 센서 반응 및 무리 호출
    protected override void HandleTargetSpotted(Transform target)
    {
        if (_currentTarget == null)
        {
            SetTarget(target);

            if (!_hasSpottedAndRoared)
            {
                _hasSpottedAndRoared = true;
                ExecuteRoarAndCallAlliesAsync(GetActionToken()).Forget();
            }
        }
    }

    protected override void HandleNoiseHeard(Vector3 noisePosition)
    {
        base.HandleNoiseHeard(noisePosition);
        if (_currentTarget == null && !_isAlerted && !_isActionExecuting)
        {
            _isAlerted = true;
            _heardNoisePosition = noisePosition;
        }
    }

    /// <summary>
    /// 주변 동료들에게 신호를 전파하는 비동기 루틴
    /// </summary>
    private async UniTaskVoid ExecuteRoarAndCallAlliesAsync(CancellationToken token)
    {
        _isActionExecuting = true;
        _movement.StopMovement();

        try
        {
            // 주변 아군 크리처에게 타깃 브로드캐스트
            BroadcastTargetToNearbyAllies();
            await PlayStateAndWaitAsync(AnimStateType.Roar, token, force: true);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _isActionExecuting = false;
        }
    }

    private void BroadcastTargetToNearbyAllies()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, _callAlliesRadius, _allyBuffer, _allyLayer);
        for (int i = 0; i < count; i++)
        {
            if (_allyBuffer[i].gameObject == gameObject) continue;

            var ally = _allyBuffer[i].GetComponentInParent<CreatureBT>();
            if (ally != null && ally.CurrentTarget == null)
            {
                ally.ReceiveAlertSignal(CurrentTarget);
            }
        }
        Array.Clear(_allyBuffer, 0, count);
    }

    /// <summary>
    /// 동료의 Roar 신호를 수신했을 때 호출:
    /// 신호를 재전파하지 않고 함께 Roar 모션을 수행한 뒤 넘겨받은 타깃으로 전투 진입
    /// </summary>
    public void ReceiveAlertSignal(Transform sharedTarget)
    {
        if (_currentTarget != null || IsDead) return;

        SetTarget(sharedTarget);
        _hasSpottedAndRoared = true; // 재전파 방지 플래그 On

        ExecuteRoarResponseAsync(GetActionToken()).Forget();
    }
    private async UniTaskVoid ExecuteRoarResponseAsync(CancellationToken token)
    {
        _isActionExecuting = true;
        _movement.StopMovement();

        try
        {
            if (_currentTarget != null)
            {
                _movement.FaceTarget(_currentTarget.position, 10.0f, Time.deltaTime);
            }

            // 재전파 없이 단독 응답 표효 모션 재생 (1.6초)
            await PlayStateAndWaitAsync(AnimStateType.Roar, token, force: true);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _isActionExecuting = false;
        }
    }
    #endregion

    public override void ClearTarget()
    {
        base.ClearTarget();
        _hasSpottedAndRoared = false;
        _isAlerted = false;
    }

    #region 크리처 전용 기즈모
    protected override void OnDrawGizmosSelected()
    {
        // 1. 공통 에너미 기즈모(접지, 시야각, 청각 반경, 순찰/리쉬 반경, 타깃선) 렌더링
        base.OnDrawGizmosSelected();

        // 2. 동료 호출(Call Allies Radius) 영역 렌더링 (보라색 와이어 구체 및 반투명 디스크)
        Gizmos.color = new Color(0.7f, 0.2f, 1.0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, _callAlliesRadius);

#if UNITY_EDITOR
        Handles.color = new Color(0.7f, 0.2f, 1.0f, 0.15f);
        Handles.DrawSolidDisc(transform.position, Vector3.up, _callAlliesRadius);
        Handles.color = new Color(0.7f, 0.2f, 1.0f, 0.6f);
        Handles.DrawWireDisc(transform.position, Vector3.up, _callAlliesRadius);
#endif
    }
    #endregion
}