/// <summary>
/// Player 회피/구르기 동작을 전담하는 Player FSM 상태 클래스
/// 진입 시 이동 입력 여부에 따라 구르기 방향 또는 백스탭을 실행
/// 스태미나 소모 및 RollForce 임펄스를 적용하고, UniTask 비동기 루틴을 통해 무적 프레임 및 후딜레이 종료 타이밍 처리
/// 동작 중 모든 조작을 금지, 종료 시점에 선입력 버퍼에 따라 상태 전환
/// </summary>
public class PlayerRollState : PlayerActionState
{
    private UnityEngine.Vector3 _rollExecutionDirection;
    private float _currentRollDuration;
    private float _elapsedRollTime;

    public bool IsInvincible { get; private set; }

    public PlayerRollState(PlayerController owner, PlayerStateMachine statMachine)
        : base(owner, statMachine)  {  }

    public override void Enter()
    {
        base.Enter();

        float staminaCost = _playerCombatConfigSO != null ? _playerCombatConfigSO.RollStaminaCost : 18.0f;

        // 1. 구르기 방향 결정
        UnityEngine.Vector3 moveInputDirection = owner.CurrentMoveDirection;
        if (moveInputDirection.sqrMagnitude > 0.001f)
        {
            _rollExecutionDirection = moveInputDirection.normalized;
            owner.transform.rotation = UnityEngine.Quaternion.LookRotation(_rollExecutionDirection);
        }
        else
        {
            // 이동 입력이 없을 경우 캐릭터의 현재 전방으로 방향 할당
            _rollExecutionDirection = owner.transform.forward;
        }

        // 2. 스태미나 차감
        _playerStatusController?.ConsumeStamina(staminaCost);
        _playerAnimController.SetTrigger(AnimParamType.Roll);

        _elapsedRollTime = 0f;
        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteRollAsync(token).Forget();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        if (_currentRollDuration > 0f)
        {
            _elapsedRollTime += UnityEngine.Time.fixedDeltaTime;
            float normalized = UnityEngine.Mathf.Clamp01(_elapsedRollTime / _currentRollDuration);

            float rollForce = (_playerStatusController != null && _playerStatusController.PlayerMotionStats != null)
                ? _playerStatusController.PlayerMotionStats.RollForce.Value : 9.0f;

            owner.ApplyRollAPI(_rollExecutionDirection, rollForce, normalized);
        }
    }

    public override void Exit()
    {
        base.Exit();
        _currentRollDuration = 0f;
        _elapsedRollTime = 0f;
        _rollExecutionDirection = UnityEngine.Vector3.zero;
        owner.StopMovementAPI();
        IsInvincible = false;
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid ExecuteRollAsync(System.Threading.CancellationToken token)
    {
        RollTier tier = owner.StatusController.PlayerMotionStats.CurrentRollTier;

        if (tier == RollTier.Overburdened)
        {
            CompleteActionAndEvaluateTransition();
            return;
        }

        RollTierConfig tierConfig 
            = owner.CombatConfig != null ? owner.CombatConfig.GetRollTierConfig(tier) : new RollTierConfig(0.35f, 0.85f, 0.72f);

        _currentRollDuration = tierConfig.TotalDuration;
        IsInvincible = true;

        // 1. 무적 시간 대기
        bool canceledAtIFrame = await Cysharp.Threading.Tasks.UniTask.Delay(System.TimeSpan.FromSeconds(tierConfig.InvincibleDuration),
            delayTiming: Cysharp.Threading.Tasks.PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
        if (canceledAtIFrame) return;

        IsInvincible = false;

        // 2. 캔슬 윈도우 대기 
        float lockoutDuration = UnityEngine.Mathf.Max(0f, tierConfig.CancelWindow - tierConfig.InvincibleDuration);

        bool canceledAtLockout = await Cysharp.Threading.Tasks.UniTask.Delay(System.TimeSpan.FromSeconds(lockoutDuration),
            delayTiming: Cysharp.Threading.Tasks.PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
        if (canceledAtLockout) return;

        // 3. 캔슬 윈도우 도달
        float remainingWindow = UnityEngine.Mathf.Max(0f, tierConfig.TotalDuration - tierConfig.CancelWindow);
        float elapsedWindow = 0f;

        while (elapsedWindow < remainingWindow)
        {
            if (token.IsCancellationRequested) return;

            // 이동 입력이 들어오면 후딜레이 캔슬
            if (owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
            {
                CompleteActionAndEvaluateTransition();
                return;
            }

            // 선입력 큐의 커맨드를 여기서 TryConsume하지 않고, 존재 여부만 감지하여 CompleteActionAndEvaluateTransition으로 일원화
            if (owner.InputReader.HasPendingCommand)
            {
                CompleteActionAndEvaluateTransition();
                return;
            }

            elapsedWindow += UnityEngine.Time.deltaTime;
            await Cysharp.Threading.Tasks.UniTask.Yield(Cysharp.Threading.Tasks.PlayerLoopTiming.Update, token).SuppressCancellationThrow();
        }

        CompleteActionAndEvaluateTransition();
    }
}
