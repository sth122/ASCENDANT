using Cysharp.Threading.Tasks;
public class PlayerStunState : PlayerActionState
{
    private System.Threading.CancellationTokenSource _stunCts;
    private const float StunDuration = 1.0f;

    public PlayerStunState(PlayerController owner, PlayerStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        CanMove = false;
        CanRotate = false;
        
        owner.StopMovementAPI();
        _animController.PlayState(AnimStateType.Stun);

        ResetToken();
        _stunCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(owner.GetCancellationTokenOnDestroy());
        ExecuteStunAsync(_stunCts.Token).Forget();
    }

    public override void Exit()
    {
        base.Exit();
        ResetToken();
    }

    private void ResetToken()
    {
        if( _stunCts != null )
        {
            _stunCts.Cancel();
            _stunCts.Dispose();
            _stunCts = null;
        }
    }

    private async UniTaskVoid ExecuteStunAsync(System.Threading.CancellationToken token)
    {
        bool canceled = await UniTask.Delay((int)(StunDuration * 1000f), delayTiming: PlayerLoopTiming.Update,
            cancellationToken: token).SuppressCancellationThrow();

        if (canceled) return;

        CompleteActionAndEvaluateTransition();
    }
}
