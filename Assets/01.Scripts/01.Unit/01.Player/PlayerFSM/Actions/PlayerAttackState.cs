/// <summary>
/// Player 공격을 전담하는 Player FSM 상태 클래스
/// </summary>
public class PlayerAttackState : PlayerActionState
{
    private int _comboStep = 1;
    private const int MaxComboStep = 3;
    private bool _hasAdvancedThisStep = false;
    private bool _isHitboxCurrentlyOpen = false;
    private bool _hasOpenedHitboxThisStep = false;
    private float _stepTimer = 0f;
    private const float StepLockoutDuration = 0.18f;
    private const float SafetyTimeout = 2.5f;

    private UnityEngine.LayerMask searchLayer = UnityEngine.LayerMask.GetMask("Enemy", "EnemyHurtbox");
    private readonly UnityEngine.Collider[] _softLockBuffer = new UnityEngine.Collider[8];
    private WeaponItemData.ComboTiming _currentTiming;

    public PlayerAttackState(PlayerController owner, PlayerStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate =false;

        // 1. 공격 진입 시 이전 지상 상태에서 누적된 잔여 공격 입력을 즉시 제거 (ComboStep = 2 건너뛰기 원천 차단)
        owner.InputReader.ClearCommand(InputCommandType.Attack);

        // 2. 스태미나 검증 (부족 시 즉시 복귀)
        float staminaCost = GetAttackStaminaCost();
        if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(staminaCost))
        {
            CompleteActionAndEvaluateTransition();
            return;
        }

        _comboStep = 1;
        _hasAdvancedThisStep = false;
        _isHitboxCurrentlyOpen = false;
        _hasOpenedHitboxThisStep = false;
        _stepTimer = 0f;

        // 3.자동 타깃 이동 입력 방향 대신 최근접/타겟팅 적을 향해 자동 스냅
        AlignAttackDirection();

        // 4. Root Motion 활성화 (클립의 전진 변위가 Rigidbody를 직접 추진하도록 권한 부여)
        _playerAnimController.SetRootMotion(true);

        // 5. 1타 실행
        ExecuteAttackStep();
    }

    public override void Update()
    {
        base.Update();

        _stepTimer += UnityEngine.Time.deltaTime;

        // 1. 비정상 동결 방어 비상 탈출
        if (_stepTimer >= SafetyTimeout)
        {
            CompleteActionAndEvaluateTransition();
            return;
        }


        UnityEngine.Animator animator = _playerAnimController.AnimatorInstance;
        if (animator.IsInTransition(0)) return;

        // 2. 타수 전환 중일 때는 히트박스 갱신 및 캔슬 판정을 전면 보류 (히트박스 조기 오픈 차단 핵심)
        if (_hasAdvancedThisStep)
        {
            CloseHitboxInternal();

            // 트랜지션이 완전히 끝나고 최소 락아웃 시간이 지났을 때만 잠금 해제
            if (_stepTimer >= StepLockoutDuration && !animator.IsInTransition(0))
            {
                _hasAdvancedThisStep = false;
            }
            return;
        }

        float normalizedTime = _playerAnimController.GetCurrentNormalizedTime(0);

        UpdateHitboxWindow(normalizedTime);

        // 3. 긴급 회피 및 가드 캔슬 판정
        if (normalizedTime >= _currentTiming.EmergencyCancelNormalized)
        {
            float rollCost = owner.CombatConfig != null ? owner.CombatConfig.RollStaminaCost : 18.0f;
            if (_playerStatusController != null && _playerStatusController.HasEnoughStamina(rollCost))
            {
                if (owner.InputReader.TryConsumeCommand(InputCommandType.Roll))
                {
                    CloseHitboxInternal();
                    _playerStateMachine.ChangeState(UnitState.Roll);
                    return;
                }
            }
            else
            {
                owner.InputReader.ClearCommand(InputCommandType.Roll);
            }

            if (owner.InputReader.TryConsumeCommand(InputCommandType.Guard))
            {
                CloseHitboxInternal();
                _playerStateMachine.ChangeState(UnitState.Guard);
                return;
            }
        }

        // 4. 콤보 연계 판정
        if (_comboStep < MaxComboStep && !_hasAdvancedThisStep)
        {
            if (normalizedTime >= _currentTiming.ComboAdvanceNormalized && normalizedTime < 0.95f)
            {
                if (owner.InputReader.TryConsumeCommand(InputCommandType.Attack))
                {
                    AdvanceCombo();
                    return;
                }
            }
        }

        // 5. 후딜레이 캔슬 이동 판정 (조작감 향상)
        if (!_hasAdvancedThisStep && normalizedTime >= _currentTiming.MovementCancelNormalized)
        {
            if (owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
            {
                CompleteActionAndEvaluateTransition();
                return;
            }
        }

        // 6. 모션 완료 시 복귀
        if (!_hasAdvancedThisStep && normalizedTime >= 0.95f)
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
        _hasOpenedHitboxThisStep = false;
        _comboStep = 0;
        _stepTimer = 0f;
    }

    /// <summary>
    /// ComboStep을 먼저 주입하고 Attack 트리거를 격발
    /// </summary>
    private void ExecuteAttackStep()
    {
        _isHitboxCurrentlyOpen = false;
        _hasOpenedHitboxThisStep = false;

        WeaponItemData weapon = owner.CombatModule.CurrentWeaponData;
        _currentTiming = (weapon != null) ? weapon.GetComboTiming(_comboStep) : new WeaponItemData.ComboTiming(0.35f, 0.55f, 0.30f, 0.65f, 0.80f);

        float staminaCost = GetAttackStaminaCost();
        _playerStatusController?.ConsumeStamina(staminaCost);

        _playerAnimController.SetInteger(AnimParamType.ComboStep, _comboStep);
        _playerAnimController.SetTrigger(AnimParamType.Attack);
    }

    /// <summary>
    /// 연타 입력 성공 시 다음 타수로 전이하는 메서드
    /// </summary>
    private void AdvanceCombo()
    {
        float nextCost = GetAttackStaminaCost();

        // 다음 타수에 필요한 스태미나가 없으면 콤보 연계를 차단하고 현재 타수의 후딜을 유지
        if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(nextCost)) return;

        CloseHitboxInternal();
        // 다음 타수가 메카님에 완전히 안착할 때까지 잠금 유지
        _hasAdvancedThisStep = true;
        _stepTimer = 0f;

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
        // 콤보 승급 대기 중일 때는 절대 열지 않음
        if (_hasAdvancedThisStep)
        {
            CloseHitboxInternal();
            return;
        }

        if (!_hasOpenedHitboxThisStep && normalizedTime >= _currentTiming.HitboxStartNormalized && normalizedTime < _currentTiming.HitboxEndNormalized)
        {
            _hasOpenedHitboxThisStep = true;
            OpenHitboxInternal();
        }
        else if (_isHitboxCurrentlyOpen && normalizedTime >= _currentTiming.HitboxEndNormalized)
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

    private float GetAttackStaminaCost()
    {
        if (owner.CombatModule != null && owner.CombatModule.CurrentWeaponData != null)
        {
            return owner.CombatModule.CurrentWeaponData.AttackStaminaCost;
        }
        return owner.CombatConfig != null ? owner.CombatConfig.AttackStaminaCost : 14.0f;
    }

    /// <summary>
    /// 공격 개시 시 락온 타깃 또는 전방 부채꼴 내 가장 가까운 적을 향해 자동 정렬
    /// </summary>
    private void AlignAttackDirection()
    {
        // 1. 이미 락온 중인 대상이 있다면 해당 대상을 정면 응시
        if (owner.CurrentLockOnTarget != null)
        {
            UnityEngine.Vector3 dirToLockOn = owner.CurrentLockOnTarget.position - owner.transform.position;
            dirToLockOn.y = 0f;
            if (dirToLockOn.sqrMagnitude > 0.001f)
            {
                owner.transform.rotation = UnityEngine.Quaternion.LookRotation(dirToLockOn.normalized);
                return;
            }
        }

        // 2. 비 락온 시: 전방 120도 부채꼴, 반경 4.0m 내의 최근접 적 탐색
        int count = UnityEngine.Physics.OverlapSphereNonAlloc(owner.transform.position, 4.0f, _softLockBuffer, searchLayer);
        UnityEngine.Transform bestTarget = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            if (_softLockBuffer[i] == null || _softLockBuffer[i].transform.root == owner.transform.root) continue;

            UnityEngine.Vector3 toEnemy = _softLockBuffer[i].transform.position - owner.transform.position;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;

            // 전방 120도 부채꼴 검사 (Dot > 0.5)
            if (UnityEngine.Vector3.Dot(owner.transform.forward, toEnemy.normalized) > 0.5f && dist < closestDist)
            {
                closestDist = dist;
                bestTarget = _softLockBuffer[i].transform;
            }
        }
        System.Array.Clear(_softLockBuffer, 0, count);

        if (bestTarget != null)
        {
            UnityEngine.Vector3 targetDir = bestTarget.position - owner.transform.position;
            targetDir.y = 0f;
            if (targetDir.sqrMagnitude > 0.001f)
            {
                owner.transform.rotation = UnityEngine.Quaternion.LookRotation(targetDir.normalized);
                return;
            }
            return;
        }

        // 3. 탐색된 적이 없다면 현재 이동 입력 방향으로 회전
        if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            owner.transform.rotation = UnityEngine.Quaternion.LookRotation(owner.CurrentMoveDirection.normalized);
        }
    }
}
