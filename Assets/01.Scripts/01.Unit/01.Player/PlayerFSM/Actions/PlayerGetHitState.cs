using Cysharp.Threading.Tasks;

public class PlayerGetHitState : PlayerActionState
{
    private System.Threading.CancellationTokenSource _hitCts;
    private const float HitStunDuration = 0.45f;

    public PlayerGetHitState(PlayerController owner, PlayerStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;
        owner.StopMovementAPI();

        _animController.SetTrigger(AnimParamType.GetHit);

        ResetToken();
        _hitCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(owner.destroyCancellationToken);
        ExecuteHitAsync(_hitCts.Token).Forget();
    }

    public override void Exit()
    {
        base.Exit();
        ResetToken();
    }

    private void ResetToken()
    {
        if (_hitCts != null)
        {
            _hitCts.Cancel();
            _hitCts.Dispose();
            _hitCts = null;
        }
    }

    private async UniTaskVoid ExecuteHitAsync(System.Threading.CancellationToken token)
    {
        bool canceled = await UniTask.Delay((int)(HitStunDuration * 1000f), 
            delayTiming: PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        CompleteActionAndEvaluateTransition();
    }
}
