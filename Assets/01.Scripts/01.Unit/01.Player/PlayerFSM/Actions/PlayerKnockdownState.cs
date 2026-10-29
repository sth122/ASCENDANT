using Cysharp.Threading.Tasks;

/// <summary>
/// 보스의 강공격 피격 시 연출되는 넉백 다운 FSM 상태
/// 물리 넉백 -> 2초간 다운(행동 불가) -> 1초간 기상 무적 처리 후 지상 기본 상태 복귀
/// </summary>
public class PlayerKnockdownState : PlayerActionState
{
    private const float DownDuration = 2.0f;
    private const float WakeUpInvincibleDuration = 1.0f;

    public bool IsInvincibleDuringWakeUp { get; private set; } = false;

    public PlayerKnockdownState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;
        IsInvincibleDuringWakeUp = false;

        // 다운 피격 모션 재생
        _playerAnimController.PlayState(AnimStateType.Death);

        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteKnockdownSequenceAsync(token).Forget();
    }

    public override void Exit()
    {
        base.Exit();
        IsInvincibleDuringWakeUp = false;
        _playerStatusController?.RestorePoiseToMax();
    }

    private async UniTaskVoid ExecuteKnockdownSequenceAsync(System.Threading.CancellationToken token)
    {
        // 1. 바닥에 쓰러져 있는 다운 시간 대기 (2초)
        bool isCanceledAtDown = await UniTask.Delay(
            System.TimeSpan.FromSeconds(DownDuration),
            cancellationToken: token
        ).SuppressCancellationThrow();

        if (isCanceledAtDown) return;

        // 2. 기상 모션 재생 및 기상 무적 윈도우 가동 (1초)
        IsInvincibleDuringWakeUp = true;
        _playerAnimController.PlayState(AnimStateType.Idle);

        bool isCanceledAtWakeUp = await UniTask.Delay(
            System.TimeSpan.FromSeconds(WakeUpInvincibleDuration),
            cancellationToken: token
        ).SuppressCancellationThrow();

        if (isCanceledAtWakeUp) return;

        IsInvincibleDuringWakeUp = false;

        // 3. 조작권 회복 및 복귀
        CompleteActionAndEvaluateTransition();
    }
}