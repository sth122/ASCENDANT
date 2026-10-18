using Cysharp.Threading.Tasks;

/// <summary>
/// 크리처 전용 포복 순찰 상태 (UnitState.Crawl <-> CreatureCrawlState)
/// </summary>
public class CreatureCrawlState : EnemyBaseState
{
    private UnityEngine.Vector3 _patrolTarget;
    private const float PatrolRadius = 8.0f;
    private const float CrawlSpeed = 1.2f;
    private bool _isWaiting;

    public CreatureCrawlState(CreatureEnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        _isWaiting = false;
        owner.StopMovementAPI();
        _enemyAnimController.PlayState(AnimStateType.CrawlIdle);

        PickNextPatrolPoint();
        _enemyAnimController.PlayState(AnimStateType.Crawl);
    }

    public override void Update()
    {
        base.Update();
        if (_isWaiting) return;

        UnityEngine.Vector3 toTarget = owner.transform.position - _patrolTarget;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= (1.0f * 1.0f))
        {
            _isWaiting = true;
            owner.StopMovementAPI();
            _enemyAnimController.PlayState(AnimStateType.CrawlIdle);

            System.Threading.CancellationToken token = CreateStateToken();
            WaitAndMoveAsync(token).Forget();
        }
        else
        {
            owner.MoveToDestinationAPI(_patrolTarget, CrawlSpeed);
        }
    }

    private async UniTaskVoid WaitAndMoveAsync(System.Threading.CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay(3000, delayTiming: PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        PickNextPatrolPoint();
        _enemyAnimController.PlayState(AnimStateType.Crawl);
        _isWaiting = false;
    }

    private void PickNextPatrolPoint()
    {
        UnityEngine.Vector2 circle = UnityEngine.Random.insideUnitCircle * PatrolRadius;
        _patrolTarget = owner.SpawnPoint + new UnityEngine.Vector3(circle.x, 0f, circle.y);
    }
}