using Cysharp.Threading.Tasks;

/// <summary>
/// 크리처 전용 포복 순찰 상태 (UnitState.Crawl <-> CreatureCrawlState)
/// </summary>
public class CreatureCrawlState : EnemyBaseState
{
    private UnityEngine.Vector3 _patrolTarget;
    private const float PatrolRadius = 8.0f;
    private const float CrawlSpeed = 2.0f;
    private const float WaitDurationAtPoint = 3.0f; // 순찰 지점 도착 후 대기 시간
    private bool _isWaiting;

    public CreatureCrawlState(CreatureEnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        _isWaiting = false;
        owner.StopMovementAPI();

        owner.SetStoppingDistanceAPI(UnitState.Patrol);

        PickNextPatrolPoint();
        _enemyAnimController.PlayState(AnimStateType.Crawl);
    }

    public override void Update()
    {
        base.Update();
        if (_isWaiting) return;

        UnityEngine.AI.NavMeshAgent agent = owner.Movement.Agent;
        if (agent == null || !agent.isOnNavMesh) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f)
        {
            _isWaiting = true;
            owner.StopMovementAPI();
            _enemyAnimController.PlayState(AnimStateType.CrawlIdle);

            System.Threading.CancellationToken token = CreateStateToken();
            WaitAndPickNextPointAsync(token).Forget();
        }
        else
        {
            owner.TickLocomotionRotationAPI(UnityEngine.Time.deltaTime, 4.0f);
        }
    }

    /// <summary>
    /// 순찰 지점에서 3초간 대기한 후 다음 순찰 지점을 선정하여 순환하는 비동기 루틴
    /// </summary>
    private async UniTaskVoid WaitAndPickNextPointAsync(System.Threading.CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay(3000, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        PickNextPatrolPoint();
        _enemyAnimController.PlayState(AnimStateType.Crawl);
        _isWaiting = false;
    }

    private void PickNextPatrolPoint()
    {
        UnityEngine.Vector2 circle = UnityEngine.Random.insideUnitCircle * PatrolRadius;
        UnityEngine.Vector3 randomPos = owner.SpawnPoint + new UnityEngine.Vector3(circle.x, 0f, circle.y);

        // 네비메시 통행 가능 좌표로 투영 보정
        if (UnityEngine.AI.NavMesh.SamplePosition(randomPos, out UnityEngine.AI.NavMeshHit hit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            _patrolTarget = hit.position;
        }
        else
        {
            _patrolTarget = owner.transform.position + owner.transform.forward * 3.0f;
        }

        owner.SetDestinationAPI(_patrolTarget, CrawlSpeed);
    }

    public override void Exit()
    {
        base.Exit();
        owner.StopMovementAPI();
        _isWaiting = false;
    }
}