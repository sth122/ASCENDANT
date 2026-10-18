/// <summary>
/// Player 공격을 전담하는 Player FSM 상태 클래스
/// </summary>
public class PlayerAttackState : PlayerActionState
{
    #region 테스트 용 변수 - 추후 PlayerCombatModule과 같은 것으로 분리해야함
    private const float BaseDamage = 35.0f;
    private const float BasePoiseDamage = 25.0f;
    private const float AttackStaminaCost = 14.0f;
    #endregion

    // 모션 진행도(normalizedTime) 기준 타임라인 설정
    private const float InputBufferStartNormalized = 0.20f; // 20% 진행 시점부터 연타 입력 수집 (Buffer Window)
    private const float ComboExecuteNormalized = 0.60f;     // 60% 시점에 콤보 캔슬 실행 (Execution Point)
    private const float ActionRecoveryNormalized = 0.90f;   // 90% 시점에 후속 연타가 없으면 즉시 조작권 회복 (Recovery)
    private const float StepLockoutDuration = 0.15f;        // 타수 전환 직후 최소 락아웃

    // 타수당 비상 안전 제한 시간 (단일 타수가 3.0초를 넘길 때만 비정상 동결로 판단하고 탈출)
    private const float SingleStepSafetyTimeout = 3.0f;

    private int _comboStep = 1;
    private const int MaxComboStep = 3;

    // 타수당 단 1회 승급 보장 및 메카님 블렌딩 완료 시까지 탈출을 차단하는 핵심 잠금 플래그
    private bool _hasAdvancedThisStep = false;
    private bool _hasQueuedCombo = false;
    private float _stepTimer = 0f;
    public PlayerAttackState(PlayerController owner, PlayerStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate =false;

        // 1. 공격 진입 시 이전 지상 상태에서 누적된 잔여 공격 입력을 즉시 제거 (ComboStep = 2 건너뛰기 원천 차단)
        owner.InputReader.ClearCommand(InputCommandType.Attack);

        // 2. 스태미나 검증 (부족 시 즉시 복귀)
        if (_playerStatController != null && !_playerStatController.HasEnoughStamina(AttackStaminaCost))
        {
            CompleteActionAndEvaluateTransition();
            return;
        }

        _comboStep = 1;
        _hasAdvancedThisStep = false;
        _hasQueuedCombo = false;
        _stepTimer = 0f;

        // 3. 공격 방향 회전 정렬
        if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            owner.transform.rotation = UnityEngine.Quaternion.LookRotation(owner.CurrentMoveDirection);
        }

        // 4. Root Motion 활성화 (클립의 전진 변위가 Rigidbody를 직접 추진하도록 권한 부여)
        _playerAnimController.SetRootMotion(true);

        EnableAttackEvent();

        // 5. 1타 실행
        ExecuteAttackStep();
    }

    public override void Update()
    {
        base.Update();

        _stepTimer += UnityEngine.Time.deltaTime;

        // 1. 현재 타수 모션이 3.0초 이상 멈춰있을 경우에만 안전 탈출
        if (_stepTimer >= SingleStepSafetyTimeout)
        {
            CompleteActionAndEvaluateTransition();
            return;
        }
        UnityEngine.Animator animator = _playerAnimController.AnimatorInstance;
        bool isInTransition = animator.IsInTransition(0);

        // 2. 메카님이 트랜지션 중(0.38초 블렌딩 중)일 때는
        // 이전 모션의 90% 시간을 읽고 캔슬되는 것을 막기 위해 탈출 검사를 일체 보류
        if (isInTransition)
        {
            return;
        }

        // 3. 다음 타수 모션에 완전히 안착하고 최소 락아웃이 지난 시점에만 잠금 플래그를 리셋
        if (_hasAdvancedThisStep && _stepTimer >= StepLockoutDuration)
        {
            _hasAdvancedThisStep = false;
        }

        // 최소 락아웃 검사
        if (_stepTimer < StepLockoutDuration)
        {
            return;
        }

        UnityEngine.AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        float normalizedTime = stateInfo.normalizedTime;

        // 4. 콤보 연계 판정 3타 미만이고 아직 다음 타수로 승급하지 않았을 때
        if (_comboStep < MaxComboStep && !_hasAdvancedThisStep)
        {
            // 80% 진행 시점에 도달했을 때, 미리 예약된 입력이 있거나 실시간 버퍼 입력이 있다면 승급
            if (normalizedTime >= ComboExecuteNormalized && normalizedTime < ActionRecoveryNormalized)
            {
                if (_hasQueuedCombo || owner.InputReader.TryConsumeCommand(InputCommandType.Attack))
                {
                    AdvanceCombo();
                    return;
                }
            }
        }

        // 5. 다음 타수 승급이 발생하지 않았고, 모션이 90%에 도달했으면 즉시 대기/이동 복귀
        if (!_hasAdvancedThisStep && normalizedTime >= ActionRecoveryNormalized)
        {
            CompleteActionAndEvaluateTransition();
            return;
        }
    }

    public override void Exit()
    {
        base.Exit();

        // 공격이 종료되면 Root Motion을 회수하여 일반 이동 제어권 복원
        _playerAnimController.SetRootMotion(false);
        _playerAnimController.WeaponHitbox?.CloseHitBox();
        DisableAttackEvent();

        // 상태 탈출 시 잔여 트리거 및 콤보 스텝 초기화
        _playerAnimController.ResetTrigger(AnimParamType.Attack);
        _playerAnimController.SetInteger(AnimParamType.ComboStep, 0);

        owner.InputReader.ClearCommand(InputCommandType.Attack);

        _hasAdvancedThisStep = false;
        _hasQueuedCombo = false;
        _comboStep = 0;
        _stepTimer = 0f;
    }

    /// <summary>
    /// ComboStep을 먼저 주입하고 Attack 트리거를 격발
    /// </summary>
    private void ExecuteAttackStep()
    {
        _stepTimer = 0f;
        _hasQueuedCombo = false;

        // 스태미나 차감
        _playerStatController?.ConsumeStamina(AttackStaminaCost);

        _playerAnimController.SetInteger(AnimParamType.ComboStep, _comboStep);
        _playerAnimController.SetTrigger(AnimParamType.Attack);
    }

    /// <summary>
    /// 연타 입력 성공 시 다음 타수로 전이하는 메서드
    /// </summary>
    private void AdvanceCombo()
    {
        // 다음 타수에 필요한 스태미나가 없으면 콤보 연계를 차단하고 현재 타수의 후딜을 유지
        if (_playerStatController != null && !_playerStatController.HasEnoughStamina(AttackStaminaCost))
        {
            _hasQueuedCombo = false;
            return;
        }

        // 다음 타수가 메카님에 완전히 안착할 때까지 잠금 유지
        _hasAdvancedThisStep = true;
        _hasQueuedCombo = false;

        // 3타 이후에는 1타로 순환(Loop)
        if (_comboStep >= MaxComboStep)
        {
            _comboStep = 1;
        }
        else
        {
            _comboStep++;
        }

        owner.InputReader.ClearCommand(InputCommandType.Attack);

        // 연타 시 입력 방향으로 재회전
        if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            owner.transform.rotation = UnityEngine.Quaternion.LookRotation(owner.CurrentMoveDirection);
        }

        ExecuteAttackStep();
    }

    #region Animation Event
    private void HandleHitboxOpen()
    {
        float multiplier = 1.0f + (_comboStep - 1) * 0.3f;
        _playerAnimController.WeaponHitbox.OpenHitBox(owner.gameObject, BaseDamage * multiplier, BasePoiseDamage * multiplier);
    }

    private void HandleHitboxClose() => _playerAnimController.WeaponHitbox?.CloseHitBox();

    private void HandleComboWindowOpen()
    {
        // 1. 회피 선입력 우선 캔슬
        if (owner.InputReader.TryConsumeCommand(InputCommandType.Roll))
        {
            _playerStateMachine.ChangeState(UnitState.Roll);
            return;
        }

        // 2. 가드/패리 선입력 캔슬
        if (owner.InputReader.TryConsumeCommand(InputCommandType.Parry))
        {
            _playerStateMachine.ChangeState(UnitState.Parry);
            return;
        }
    }

    private void HandleComboWindowClose() { }

    private void HandleActionCompleted()
    {
        if (!_hasAdvancedThisStep)
        {
            CompleteActionAndEvaluateTransition();
        }
    }
    #endregion

    #region Input Event
    private void HandleCommandDuringAttack(InputCommandType command)
    {
        switch (command)
        {
            case InputCommandType.Roll:
                // 모션 50% 이후 긴급 회피 캔슬 허용
                if (_playerAnimController.GetCurrentNormalizedTime(0) >= InputBufferStartNormalized)
                {
                    owner.InputReader.ClearCommand(InputCommandType.Roll);
                    _playerStateMachine.ChangeState(UnitState.Roll);
                }
                break;

            case InputCommandType.Parry:
                // 모션 50% 이후 긴급 가드 캔슬 허용
                if (_playerAnimController.GetCurrentNormalizedTime(0) >= InputBufferStartNormalized)
                {
                    owner.InputReader.ClearCommand(InputCommandType.Parry);
                    _playerStateMachine.ChangeState(UnitState.Parry);
                }
                break;

            case InputCommandType.Attack:
                // 3타(피니셔) 완주 중에는 연타 예약 차단
                if (_comboStep >= MaxComboStep) return;

                // 공격 도중 들어온 모든 연타 클릭은 100% 예약 수집
                _hasQueuedCombo = true;
                break;
        }
    }
    #endregion

    private void EnableAttackEvent()
    {
        DisableAttackEvent();
        _playerAnimController.OnHitboxOpenEvent += HandleHitboxOpen;
        _playerAnimController.OnHitboxCloseEvent += HandleHitboxClose;
        _playerAnimController.OnComboWindowOpenEvent += HandleComboWindowOpen;
        _playerAnimController.OnComboWindowCloseEvent += HandleComboWindowClose;
        _playerAnimController.OnActionCompletedEvent += HandleActionCompleted;

        owner.InputReader.OnCommandInputEvent += HandleCommandDuringAttack;
    }
    private void DisableAttackEvent()
    {
        _playerAnimController.OnHitboxOpenEvent -= HandleHitboxOpen;
        _playerAnimController.OnHitboxCloseEvent -= HandleHitboxClose;
        _playerAnimController.OnComboWindowOpenEvent -= HandleComboWindowOpen;
        _playerAnimController.OnComboWindowCloseEvent -= HandleComboWindowClose;
        _playerAnimController.OnActionCompletedEvent -= HandleActionCompleted;

        owner.InputReader.OnCommandInputEvent -= HandleCommandDuringAttack;
    }
}
