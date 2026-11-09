using Cysharp.Threading.Tasks;

/// <summary>
/// 보스의 강공격 피격 시 연출되는 넉백 다운 FSM 상태
/// 물리 넉백 -> 2초간 다운(행동 불가) -> 1초간 기상 무적 처리 후 지상 기본 상태 복귀
/// </summary>
public class PlayerKnockbackState : PlayerActionState
{
    public bool IsInvincible { get; private set; } = false;

    public PlayerKnockbackState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;
        IsInvincible = false;

        // 1. 피격 직전 누적된 선입력 커맨드 즉시 전량 폐기
        owner.InputReader.ClearAllCommands();

        // 2. 넉다운 애니메이션 트리거 격발
        _playerAnimController.SetTrigger(AnimParamType.Knockback);

        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteKnockbackSequenceAsync(token).Forget();
    }

    public override void Update()
    {
        base.Update();

        // 다운 및 기상 도중 연타된 커맨드가 큐에 누적되지 않도록 버퍼 청소
        owner.InputReader.ClearAllCommands();
    }

    public override void Exit()
    {
        base.Exit();
        _playerAnimController.ResetTrigger(AnimParamType.Knockback);
        _playerAnimController.ResetTrigger(AnimParamType.WakeUp);

        // 다운 종료 후 강인도(Poise)를 최대치로 즉시 복원하여 기상 직후 연속 경직 방지
        _playerStatusController?.ResetPoiseToDefault();

        // 최종 버퍼 비우기
        owner.InputReader.ClearAllCommands();
    }

    /// <summary>
    /// 실제 애니메이션 클립 길이 및 물리 지면 접지를 기반으로 구동되는 완전 동적 시퀀스
    /// </summary>
    private async UniTaskVoid ExecuteKnockbackSequenceAsync(System.Threading.CancellationToken token)
    {
        // 1. AnimController로부터 Knockback 실제 애니메이션 재생 길이 동적 쿼리
        float knockbackClipDuration = _playerAnimController.GetStateDuration(AnimStateType.Knockback);
        float elapsedKnockbackTime = 0f;
        bool hasLanded = false;

        // 2. 물리적 착지 감지 루프
        // 공중에 떠 있는 동안 매 프레임 접지 상태를 확인하고, 바닥에 닿는 순간 관성 제동
        while (elapsedKnockbackTime < knockbackClipDuration)
        {
            bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (isCanceled) return;

            elapsedKnockbackTime += UnityEngine.Time.deltaTime;

            if (!hasLanded && elapsedKnockbackTime > 0.1f) // 날아오르는 극초반 0.1초는 접지 체크 제외
            {
                owner.Movement.CheckGrounded();
                if (owner.Movement.IsGrounded)
                {
                    hasLanded = true;
                    // 착지 순간 수평 관성 속도를 제거하여 바닥 미끄러짐 방지
                    owner.StopMovementAPI();
                }
            }
        }

        // 3. 지면 제동 최종 보장
        owner.StopMovementAPI();

        // 4. 기상(WakeUp) 트리거 격발
        owner.InputReader.ClearAllCommands();
        _playerAnimController.ResetTrigger(AnimParamType.Knockback);
        _playerAnimController.SetTrigger(AnimParamType.WakeUp);

        // 5. AnimController로부터 WakeUp 실제 애니메이션 재생 길이 동적 쿼리
        float wakeUpClipDuration = _playerAnimController.GetStateDuration(AnimStateType.WakeUp);

        // 기상 모션 재생 시간만큼 무적을 유지하며 대기
        bool isCanceledAtWakeUp = await UniTask.Delay(System.TimeSpan.FromSeconds(wakeUpClipDuration),
            delayTiming: PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();

        if (isCanceledAtWakeUp) return;

        // 6. 기상 완료 직후 무적 해제 및 제어권 복귀
        IsInvincible = false;
        CompleteActionAndEvaluateTransition();
    }
}