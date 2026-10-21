public class PlayerStunState : PlayerActionState
{
    private const float StunDuration = 1.0f;

    public PlayerStunState(PlayerController owner, PlayerStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        CanMove = false;
        CanRotate = false;
        
        owner.StopMovementAPI();
        _playerAnimController.PlayState(AnimStateType.Stun);

        // 스턴 상태 진입 시 Poise 자동 회복 일시 정지
        _playerStatusController?.SetPoiseRecoveryPause(true);

        ResetActionToken();
        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteStunAsync(token).Forget();
    }

    public override void Exit()
    {
        base.Exit();

        // 스턴 종료 시 Poise 회복 루프 실행
        _playerStatusController?.RestorePoiseToMax();
        ResetActionToken();
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid ExecuteStunAsync(System.Threading.CancellationToken token)
    {
        bool canceled = await Cysharp.Threading.Tasks.UniTask.Delay((int)(StunDuration * 1000f),
            cancellationToken: token).SuppressCancellationThrow();

        if (canceled) return;

        CompleteActionAndEvaluateTransition();
    }
}
