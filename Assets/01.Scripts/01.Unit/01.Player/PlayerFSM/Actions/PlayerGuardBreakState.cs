public class PlayerGuardBreakState : PlayerActionState
{
    private const float GuardBreakDuration = 1.5f;

    public PlayerGuardBreakState(PlayerController owner, PlayerStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        CanMove = false;
        CanRotate = false;
        
        owner.StopMovementAPI();

        // 1. 가드 상태 강제 해제 및 가드 파쇄 모션 재생
        _playerAnimController.SetBool(AnimParamType.IsGuarding, false);
        _playerAnimController.SetTrigger(AnimParamType.GuardBreak);

        // 2. 스턴 상태 진입 시 Poise 자동 회복 일시 정지
        _playerStatusController?.SetPoiseRecoveryPause(true);

        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteStunAsync(token).Forget();
    }

    public override void Exit()
    {
        base.Exit();

        // 스턴 종료 시 Poise 회복 루프 실행
        _playerAnimController.ResetTrigger(AnimParamType.GuardBreak);
        _playerStatusController?.SetPoiseRecoveryPause(false);
        ResetActionToken();
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid ExecuteStunAsync(System.Threading.CancellationToken token)
    {
        bool canceled = await Cysharp.Threading.Tasks.UniTask.Delay(System.TimeSpan.FromSeconds(GuardBreakDuration),
            cancellationToken: token ).SuppressCancellationThrow();

        if (canceled) return;

        CompleteActionAndEvaluateTransition();
    }
}
