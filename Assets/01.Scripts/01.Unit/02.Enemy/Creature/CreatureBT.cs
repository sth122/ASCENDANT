using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
        deathSeq.Add(new PredicateNode<CreatureBT>(this, static c => c.IsDead));
        deathSeq.Add(new ActionNode(ExecuteDeath));
        root.Add(deathSeq);

        // 2. 그로기 브랜치
        var groggySeq = new SequenceNode();
        groggySeq.Add(new PredicateNode<CreatureBT>(this, static c =>
            c._enemyStatus != null && (c._groggyModule.IsGroggy || c._enemyStatus.IsPoiseBroken)));
        groggySeq.Add(new ActionNode(TickGroggyStatus));
        root.Add(groggySeq);

        // 3. 발소리 감지 경계 (Alert) 브랜치
        var alertSeq = new SequenceNode();
        alertSeq.Add(new PredicateNode<CreatureBT>(this, static c =>
            !c.IsActionExecuting && c._isAlerted && c._currentTarget == null));
        alertSeq.Add(new AsyncActionNode(ExecuteAlertSequenceAsync, GetActionToken));
        root.Add(alertSeq);

        // 4. 복귀 분기
        var returnSeq = new SequenceNode();
        returnSeq.Add(new PredicateNode<CreatureBT>(this, static c =>
            !c.IsActionExecuting && c._patrolModule.CheckShouldReturn(c._enemyStatus.EnemyInfoSource != null ? c._enemyStatus.EnemyInfoSource.MaxLeashDistance : 25f)));
        returnSeq.Add(new ActionNode(TickReturnToSpawn));
        root.Add(returnSeq);

        // 5. 전투 브랜치
        var combatBranch = new SequenceNode();
        combatBranch.Add(new PredicateNode<CreatureBT>(this, static c => c._currentTarget != null && !c._groggyModule.IsGroggy));
        combatBranch.Add(BuildCombatSelector());
        root.Add(combatBranch);

        // 6. 순찰 브랜치
        root.Add(new ActionNode(TickPatrol));

        return root;
    }

    protected override async UniTaskVoid ExecuteSpawnSequenceAsync(CancellationToken token)
    {
        SetActionState(EnemyActionState.Spawning);
        _movement.StopMovement();

        try
        {
            await PlayStateAndWaitAsync(AnimStateType.Roar, token, force: true);
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            if (ActionState == EnemyActionState.Spawning)
            {
                SetActionState(EnemyActionState.None);
            }
        }

        AcquireNearestTarget();
    }

    private async UniTask<bool> ExecuteAlertSequenceAsync(CancellationToken ct)
    {
        SetActionState(EnemyActionState.Alerting);
        _movement.StopMovement();

        try
        {
            Vector3 toNoise = _heardNoisePosition - transform.position;
            toNoise.y = 0f;
            if (toNoise.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(toNoise.normalized);
            }

            await PlayStateAndWaitAsync(AnimStateType.CrawlToState, ct, force: true);

            // 경계 기립 직후 타깃을 발견했다면 즉시 경계 루틴을 종료하고 전투 포효로 인계
            if (_currentTarget != null) return true;

            await PlayStateAndWaitAsync(AnimStateType.Idle, ct, force: true);
            if (_currentTarget != null) return true;

            await PlayStateAndWaitAsync(AnimStateType.StateToCrawl, ct, force: true);
        }
        catch (OperationCanceledException) { return false; }
        finally
        {
            _isAlerted = false;
            // 다른 상태(Roar/Groggy/Dead)로 이미 전이된 경우 덮어쓰지 않음
            if (ActionState == EnemyActionState.Alerting && !_hasSpottedAndRoared)
            {
                SetActionState(EnemyActionState.None);
            }
        }

        return true;
    }

    protected override void HandleTargetSpotted(Transform target)
    {
        if (IsDead || target == null) return;

        if (_currentTarget == null)
        {
            SetTarget(target);
            _isAlerted = false;

            if (!_hasSpottedAndRoared && !_groggyModule.IsGroggy)
            {
                _hasSpottedAndRoared = true;
                ExecuteRoarAndCallAlliesAsync(GetActionToken()).Forget();
            }
        }
    }

    protected override void HandleNoiseHeard(Vector3 noisePosition)
    {
        if (_currentTarget != null || IsActionExecuting || IsDead || _groggyModule.IsGroggy) return;
        _isAlerted = true;
        _heardNoisePosition = noisePosition;
    }

    private async UniTaskVoid ExecuteRoarAndCallAlliesAsync(CancellationToken token)
    {
        SetActionState(EnemyActionState.Alerting);
        _movement.StopMovement();

        try
        {
            BroadcastTargetToNearbyAllies();

            float roarDuration = _enemyAnim.GetStateDuration(AnimStateType.Roar);
            _enemyAnim.PlayState(AnimStateType.Roar, force: true);

            float elapsed = 0f;
            while (elapsed < roarDuration)
            {
                token.ThrowIfCancellationRequested();
                if (_currentTarget != null)
                {
                    _movement.FaceTarget(_currentTarget.position, 8.0f, Time.deltaTime);
                }
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            if (ActionState == EnemyActionState.Alerting)
            {
                SetActionState(EnemyActionState.None);
            }
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

    public void ReceiveAlertSignal(Transform sharedTarget)
    {
        if (_currentTarget != null || IsDead || _groggyModule.IsGroggy) return;
        if (ActionState == EnemyActionState.BasicAttacking || ActionState == EnemyActionState.SkillCasting) return;

        SetTarget(sharedTarget);
        _isAlerted = false;
        _hasSpottedAndRoared = true;
        ExecuteRoarResponseAsync(GetActionToken()).Forget();
    }

    private async UniTaskVoid ExecuteRoarResponseAsync(CancellationToken token)
    {
        SetActionState(EnemyActionState.Alerting);
        _movement.StopMovement();

        try
        {
            float roarDuration = _enemyAnim.GetStateDuration(AnimStateType.Roar);
            _enemyAnim.PlayState(AnimStateType.Roar, force: true);

            float elapsed = 0f;
            while (elapsed < roarDuration)
            {
                token.ThrowIfCancellationRequested();
                if (_currentTarget != null)
                {
                    _movement.FaceTarget(_currentTarget.position, 10.0f, Time.deltaTime);
                }
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            if (ActionState == EnemyActionState.Alerting)
            {
                SetActionState(EnemyActionState.None);
            }
        }
    }

    public override void ClearTarget()
    {
        base.ClearTarget();
        _hasSpottedAndRoared = false;
        _isAlerted = false;
    }
}