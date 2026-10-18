using Cysharp.Threading.Tasks;

public class EnemyGetHitState : EnemyBaseState
{
    private const float HitStunDuration = 0.5f;

    public EnemyGetHitState(EnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        owner.StopMovementAPI();
        _enemyAnimController.PlayState(AnimStateType.Hit1);

        System.Threading.CancellationToken token = CreateStateToken();
        WaitForStunRecoveryAsync(token).Forget();
    }

    private async UniTaskVoid WaitForStunRecoveryAsync(System.Threading.CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay((int)(HitStunDuration * 1000f), delayTiming: PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        _enemyStateMachine.ChangeState((int)UnitState.Chase);
    }
}