using Cysharp.Threading.Tasks;

/// <summary>
/// 크리처 전용 기상 수색 상태 (UnitState.Alert <-> CreatureAlertState)
/// </summary>
public class CreatureAlertState : EnemyBaseState
{
    public CreatureAlertState(CreatureEnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        owner.StopMovementAPI();

        System.Threading.CancellationToken token = CreateStateToken();
        ExecuteAlertSequenceAsync(token).Forget();
    }

    private async UniTaskVoid ExecuteAlertSequenceAsync(System.Threading.CancellationToken token)
    {
        // 1. 포복에서 서기 전환 모션
        _enemyAnimController.PlayState(AnimStateType.CrawlToState);

        bool isCanceled = await UniTask.Delay(1200, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // 2. 서서 대기 모션
        _enemyAnimController.PlayState(AnimStateType.BattleIdle);

        // 3. 5초간 수색 대기
        isCanceled = await UniTask.Delay(5000, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // 4. 서기에서 포복 복귀 모션
        _enemyAnimController.PlayState(AnimStateType.StateToCrawl);

        isCanceled = await UniTask.Delay(1200, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // 5. 포복 순찰 복귀
        _enemyStateMachine.ChangeState((int)UnitState.Crawl);
    }
}