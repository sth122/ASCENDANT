using Cysharp.Threading.Tasks;

/// <summary>
/// 높은 곳에서 낙하 후 지면 착지 시 발생하는 하드 랜딩 경직 상태 (0.6초간 조작 차단)
/// </summary>
public class PlayerHardLandingState : PlayerActionState
{
    private const float HardLandingDuration = 0.60f;

    public PlayerHardLandingState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;
        owner.StopMovementAPI();

        // 1. 하드 랜딩 착지 전용 트리거 및 상태 격발
        _playerAnimController.SetTrigger(AnimParamType.HardLanding);
        _playerAnimController.PlayState(AnimStateType.HardLanding, force: true);

        // 2. 0.6초 대기 후 제어권 복귀 시퀀스
        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteLandingRecoveryAsync(token).Forget();
    }

    private async UniTaskVoid ExecuteLandingRecoveryAsync(System.Threading.CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay(
            System.TimeSpan.FromSeconds(HardLandingDuration),
            delayTiming: PlayerLoopTiming.Update,
            cancellationToken: token).SuppressCancellationThrow();

        if (isCanceled) return;

        CompleteActionAndEvaluateTransition();
    }

    public override void Exit()
    {
        base.Exit();
        _playerAnimController.ResetTrigger(AnimParamType.HardLanding);
    }
}