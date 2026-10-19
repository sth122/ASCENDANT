/// <summary>
/// Player 공격을 전담하는 Player FSM 상태 클래스
/// </summary>
public class PlayerAttackState : PlayerActionState
{
    private const float AttackStaminaCost = 14.0f;

    // 모션 진행도(normalizedTime) 기준 타임라인 설정
    private const float HitboxOpenNormalized = 0.22f;       // 타격 판정 시작
    private const float HitboxCloseNormalized = 0.60f;      // 타격 판정 종료
    private const float EmergencyCancelNormalized = 0.35f;  // 긴급 회피/패링 허용 시작
    private const float ComboAdvanceStartNormalized = 0.50f;// 후속 공격 입력 수집 및 승급 시작
    private const float ActionRecoveryNormalized = 0.88f;   // 모션 완주 후 대기/이동 복귀
    private const float StepLockoutDuration = 0.15f;        // 타수 전환 직후 잠금

    // 타수당 비상 안전 제한 시간 (단일 타수가 3.0초를 넘길 때만 비정상 동결로 판단하고 탈출)
    private const float SafetyTimeout = 3.0f;

    private int _comboStep = 1;
    private const int MaxComboStep = 3;

    // 타수당 단 1회 승급 보장 및 메카님 블렌딩 완료 시까지 탈출을 차단하는 핵심 잠금 플래그
    private bool _hasAdvancedThisStep = false;
    private bool _isHitboxCurrentlyOpen = false;
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
        _isHitboxCurrentlyOpen = false;
        _stepTimer = 0f;

        // 3. 공격 방향 회전 정렬
        if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            owner.transform.rotation = UnityEngine.Quaternion.LookRotation(owner.CurrentMoveDirection);
        }

        // 4. Root Motion 활성화 (클립의 전진 변위가 Rigidbody를 직접 추진하도록 권한 부여)
        _playerAnimController.SetRootMotion(true);

        // 5. 1타 실행
        ExecuteAttackStep();
    }

    public override void Update()
    {
        base.Update();

        _stepTimer += UnityEngine.Time.deltaTime;

        // 1. 현재 타수 모션이 3.0초 이상 멈춰있을 경우에만 안전 탈출
        if (_stepTimer >= SafetyTimeout)
        {
            CompleteActionAndEvaluateTransition();
            return;
        }


        UnityEngine.Animator animator = _playerAnimController.AnimatorInstance;
        // 2. 메카님이 트랜지션 중(0.38초 블렌딩 중)일 때는
        // 이전 모션의 88% 시간을 읽고 캔슬되는 것을 막기 위해 탈출 검사를 일체 보류
        if (animator.IsInTransition(0)) return;

        // 3. 다음 타수 모션에 완전히 안착하고 최소 락아웃이 지난 시점에만 잠금 플래그를 리셋
        if (_hasAdvancedThisStep && _stepTimer >= StepLockoutDuration)
        {
            _hasAdvancedThisStep = false;
        }

        // 최소 락아웃 검사
        if (_stepTimer < StepLockoutDuration) return;

        float normalizedTime = _playerAnimController.GetCurrentNormalizedTime(0);

        // 히트 박스 처리
        UpdateHitboxWindow(normalizedTime);

        // 4. 긴급 회피 및 가드 선입력 캔슬 판정
        if (normalizedTime >= EmergencyCancelNormalized)
        {
            if (owner.InputReader.TryConsumeCommand(InputCommandType.Roll))
            {
                CloseHitboxInternal();
                _playerStateMachine.ChangeState(UnitState.Roll);
                return;
            }

            if (owner.InputReader.TryConsumeCommand(InputCommandType.Parry))
            {
                CloseHitboxInternal();
                _playerStateMachine.ChangeState(UnitState.Parry);
                return;
            }
        }

        // 5. 콤보 연계 판정 3타 미만이고 아직 다음 타수로 승급하지 않았을 때
        if (_comboStep < MaxComboStep && !_hasAdvancedThisStep)
        {
            // 80% 진행 시점에 도달했을 때, 미리 예약된 입력이 있거나 실시간 버퍼 입력이 있다면 승급
            if (normalizedTime >= ComboAdvanceStartNormalized && normalizedTime < ActionRecoveryNormalized)
            {
                if (owner.InputReader.TryConsumeCommand(InputCommandType.Attack))
                {
                    AdvanceCombo();
                    return;
                }
            }
        }

        // 6. 다음 타수 승급이 발생하지 않았고, 모션이 90%에 도달했으면 즉시 대기/이동 복귀
        if (!_hasAdvancedThisStep && normalizedTime >= ActionRecoveryNormalized)
        {
            CompleteActionAndEvaluateTransition();
        }
    }

    public override void Exit()
    {
        base.Exit();

        // 공격이 종료되면 Root Motion을 회수하여 일반 이동 제어권 복원
        _playerAnimController.SetRootMotion(false);
        CloseHitboxInternal();

        // 상태 탈출 시 잔여 트리거 및 콤보 스텝 초기화
        _playerAnimController.ResetTrigger(AnimParamType.Attack);
        _playerAnimController.SetInteger(AnimParamType.ComboStep, 0);

        owner.InputReader.ClearCommand(InputCommandType.Attack);

        _hasAdvancedThisStep = false;
        _isHitboxCurrentlyOpen = false;
        _comboStep = 0;
        _stepTimer = 0f;
    }

    /// <summary>
    /// ComboStep을 먼저 주입하고 Attack 트리거를 격발
    /// </summary>
    private void ExecuteAttackStep()
    {
        _stepTimer = 0f;
        _isHitboxCurrentlyOpen = false
            ;
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
        if (_playerStatController != null && !_playerStatController.HasEnoughStamina(AttackStaminaCost)) return;

        CloseHitboxInternal();
        // 다음 타수가 메카님에 완전히 안착할 때까지 잠금 유지
        _hasAdvancedThisStep = true;

        // 3타 이후에는 1타로 순환(Loop)
        _comboStep = (_comboStep >= MaxComboStep) ? 1 : _comboStep + 1;

        owner.InputReader.ClearCommand(InputCommandType.Attack);

        // 연타 시 입력 방향으로 재회전
        if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            owner.transform.rotation = UnityEngine.Quaternion.LookRotation(owner.CurrentMoveDirection);
        }

        ExecuteAttackStep();
    }

    private void UpdateHitboxWindow(float normalizedTime)
    {
        if (!_isHitboxCurrentlyOpen && normalizedTime >= HitboxOpenNormalized && normalizedTime < HitboxCloseNormalized)
        {
            OpenHitboxInternal();
        }
        else if (_isHitboxCurrentlyOpen && normalizedTime >= HitboxCloseNormalized)
        {
            CloseHitboxInternal();
        }
    }

    private void OpenHitboxInternal()
    {
        if (_isHitboxCurrentlyOpen) return;
        _isHitboxCurrentlyOpen = true;
        owner.CombatModule.OpenWeaponHitbox(_comboStep);
    }

    private void CloseHitboxInternal()
    {
        if (!_isHitboxCurrentlyOpen) return;
        _isHitboxCurrentlyOpen = false;
        owner.CombatModule.CloseWeaponHitbox();
    }
}
