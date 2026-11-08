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
            c._enemyStatus != null && c._enemyStatus.IsPoiseBroken && !c._groggyModule.IsGroggy));
        groggySeq.Add(new ActionNode(TriggerGroggyAction));
        root.Add(groggySeq);

        // 3. 발소리 감지 수색 (Alert) 브랜치
        var alertSeq = new SequenceNode();
        alertSeq.Add(new PredicateNode<CreatureBT>(this, static c => !c.IsActionExecuting && c._isAlerted));
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
        combatBranch.Add(new PredicateNode<CreatureBT>(this, static c => c._currentTarget != null));
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
            SetActionState(EnemyActionState.None);
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
            await PlayStateAndWaitAsync(AnimStateType.Idle, ct, force: true);
            await PlayStateAndWaitAsync(AnimStateType.StateToCrawl, ct, force: true);
        }
        finally
        {
            _isAlerted = false;
            SetActionState(EnemyActionState.None);
        }

        return true;
    }

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
        if (_currentTarget == null && !_isAlerted && !IsActionExecuting)
        {
            _isAlerted = true;
            _heardNoisePosition = noisePosition;
        }
    }

    private async UniTaskVoid ExecuteRoarAndCallAlliesAsync(CancellationToken token)
    {
        SetActionState(EnemyActionState.Alerting);
        _movement.StopMovement();

        try
        {
            BroadcastTargetToNearbyAllies();
            await PlayStateAndWaitAsync(AnimStateType.Roar, token, force: true);
        }
        catch (OperationCanceledException) { }
        finally
        {
            SetActionState(EnemyActionState.None);
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
        if (_currentTarget != null || IsDead || IsActionExecuting) return;

        SetTarget(sharedTarget);
        _hasSpottedAndRoared = true;
        ExecuteRoarResponseAsync(GetActionToken()).Forget();
    }

    private async UniTaskVoid ExecuteRoarResponseAsync(CancellationToken token)
    {
        SetActionState(EnemyActionState.Alerting);
        _movement.StopMovement();

        try
        {
            if (_currentTarget != null)
            {
                _movement.FaceTarget(_currentTarget.position, 10.0f, Time.deltaTime);
            }

            await PlayStateAndWaitAsync(AnimStateType.Roar, token, force: true);
        }
        catch (OperationCanceledException) { }
        finally
        {
            SetActionState(EnemyActionState.None);
        }
    }

    public override void ClearTarget()
    {
        base.ClearTarget();
        _hasSpottedAndRoared = false;
        _isAlerted = false;
    }
}