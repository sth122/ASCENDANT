using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 에너미의 스폰 좌표 보존, 리쉬 한계선 평가, 스폰 지점 복귀 및 NavMesh 랜덤 순찰을 전담하는 모듈
/// </summary>
public class EnemyPatrolModule : UnitBaseModule, IDisposable
{
    private readonly EnemyBTController _owner;
    private readonly NavMeshAgent _agent;
    private readonly EnemyMovement _movement;
    private readonly EnemyAnimController _anim;

    private Vector3 _spawnPoint;
    private Vector3 _patrolTarget;
    private bool _isReturning = false;
    private bool _isWaiting = false;
    private CancellationTokenSource _patrolCts;

    public Vector3 SpawnPoint => _spawnPoint;
    public bool IsReturning => _isReturning;

    public EnemyPatrolModule(EnemyBTController owner, NavMeshAgent agent, EnemyMovement movement, EnemyAnimController anim)
    {
        _owner = owner;
        _agent = agent;
        _movement = movement;
        _anim = anim;
        _spawnPoint = owner.transform.position;
    }

    public void SetSpawnPoint(Vector3 point)
    {
        _spawnPoint = point;
        _isReturning = false;
    }

    public bool CheckShouldReturn(float maxLeash)
    {
        if (_isReturning) return true;

        Vector3 diff = _owner.transform.position - _spawnPoint;
        diff.y = 0f;

        if (diff.sqrMagnitude > (maxLeash * maxLeash))
        {
            _isReturning = true;
            _owner.ClearTarget();
            return true;
        }
        return false;
    }

    public INode.ENodeState TickReturn(float walkSpeed)
    {
        _owner.ClearTarget();
        _owner.ReleaseAttackToken();

        _movement.SetDestination(_spawnPoint, walkSpeed);
        _movement.TickLocomotionRotation(Time.deltaTime, 4.0f);
        _anim.PlayState(AnimStateType.Walk);

        Vector3 toSpawn = _owner.transform.position - _spawnPoint;
        toSpawn.y = 0f;

        if (toSpawn.sqrMagnitude <= (1.2f * 1.2f))
        {
            _movement.StopMovement();
            _isReturning = false;
            return INode.ENodeState.ENS_Success;
        }

        return INode.ENodeState.ENS_Running;
    }

    public INode.ENodeState TickPatrol(float patrolSpeed, float maxLeash)
    {
        if (_isWaiting) return INode.ENodeState.ENS_Running;

        if (_agent != null) _agent.stoppingDistance = 0.5f;

        if (!_agent.pathPending && _agent.hasPath && _agent.remainingDistance <= _agent.stoppingDistance + 0.3f)
        {
            _isWaiting = true;
            _movement.StopMovement();
            _anim.PlayState(AnimStateType.PatrolIdle);

            WaitAndPickNextPointAsync(patrolSpeed, maxLeash).Forget();
        }
        else
        {
            _movement.TickLocomotionRotation(Time.deltaTime, 3.5f);
            _anim.PlayState(AnimStateType.Patrol);
        }

        return INode.ENodeState.ENS_Running;
    }

    private async UniTaskVoid WaitAndPickNextPointAsync(float patrolSpeed, float maxLeash)
    {
        AsyncUtil.CancelAndDispose(ref _patrolCts);
        _patrolCts = CancellationTokenSource.CreateLinkedTokenSource(_owner.destroyCancellationToken);

        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(2.5f), cancellationToken: _patrolCts.Token).SuppressCancellationThrow();
        if (canceled) return;

        PickNextPatrolPoint(patrolSpeed, maxLeash);
        _isWaiting = false;
    }

    public void PickNextPatrolPoint(float patrolSpeed, float maxLeash)
    {
        float safeRadius = maxLeash * 0.4f;
        Vector3 finalPoint = _spawnPoint;

        for (int i = 0; i < 3; i++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * safeRadius;
            Vector3 candidate = _spawnPoint + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3.5f, NavMesh.AllAreas))
            {
                finalPoint = hit.position;
                break;
            }
        }

        _patrolTarget = finalPoint;
        _movement.SetDestination(_patrolTarget, patrolSpeed);
    }

    public void ResetState()
    {
        AsyncUtil.CancelAndDispose(ref _patrolCts);
        _isReturning = false;
        _isWaiting = false;
    }

    public void Dispose() => AsyncUtil.CancelAndDispose(ref _patrolCts);
}