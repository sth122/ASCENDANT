using Cysharp.Threading.Tasks;

public class PlayerGetHitState : PlayerActionState
{
    private const float HitStunDuration = 0.45f;

    public PlayerGetHitState(PlayerController owner, PlayerStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;
        owner.StopMovementAPI();

        _playerAnimController.SetTrigger(AnimParamType.GetHit);

        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteHitAsync(token).Forget();
    }

    public override void Exit()
    {
        base.Exit();
    }

    private async UniTaskVoid ExecuteHitAsync(System.Threading.CancellationToken token)
    {
        bool canceled = await UniTask.Delay((int)(HitStunDuration * 1000f), 
            delayTiming: PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        CompleteActionAndEvaluateTransition();
    }
}
