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
    private float _currentAttackSpeedMult = 1.0f;

    // 직전 상태(Locomotion/이전 타수)의 잔여 normalizedTime 오판 방지용 상태 추적 플래그
    private int _previousStateHash = 0;
    private bool _sawTransition = false;
    private bool _hasEnteredCurrentAttackClip = false;

    private UnityEngine.LayerMask searchLayer = UnityEngine.LayerMask.GetMask("Enemy", "EnemyHurtbox");
    private readonly UnityEngine.Collider[] _softLockBuffer = new UnityEngine.Collider[8];
    private WeaponItemData.ComboTiming _currentTiming;

    public PlayerAttackState(PlayerController owner, PlayerStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;

        // 1. 공격 진입 시 이전 지상 상태에서 누적된 잔여 공격 입력을 즉시 제거
        owner.InputReader.ClearCommand(InputCommandType.Attack);

        // 2. 실제 장착 무기 기준 스태미나 검증
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

        // 3. 락온 이동 파라미터 잔류 차단 후 타깃 정렬
        _playerAnimController.SetBool(AnimParamType.IsLockOn, false);
        AlignAttackDirection();

        // 4. Root Motion 활성화
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
        if (animator == null) return;

        if (animator.IsInTransition(0))
        {
            _sawTransition = true;
            return;
        }

        // 아직 새 공격 클립으로 전환이 완료되지 않았다면 이전 상태의 normalizedTime으로 종료/캔슬 판정 금지
        if (!_hasEnteredCurrentAttackClip)
        {
            int currentHash = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            if (_sawTransition || currentHash != _previousStateHash)
            {
                _hasEnteredCurrentAttackClip = true;
            }
            else
            {
                return;
            }
        }

        // 2. 공격 속도 배율에 맞춰 콤보 전환 락아웃 시간 비례 보정
        float scaledLockout = StepLockoutDuration / UnityEngine.Mathf.Max(1.0f, _currentAttackSpeedMult);
        if (_hasAdvancedThisStep)
        {
            CloseHitboxInternal();
            if (_stepTimer >= scaledLockout)
            {
                _hasAdvancedThisStep = false;
            }
            return;
        }

        float normalizedTime = _playerAnimController.GetCurrentNormalizedTime(0);
        UpdateHitboxWindow(normalizedTime);

        // 3. 콤보 연계 판정
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

        // 4. 후딜레이 캔슬 이동 판정
        if (!_hasAdvancedThisStep && normalizedTime >= _currentTiming.MovementCancelNormalized)
        {
            if (owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
            {
                CompleteActionAndEvaluateTransition();
                return;
            }
        }

        // 5. 모션 완료 시 복귀
        if (!_hasAdvancedThisStep && normalizedTime >= 0.95f)
        {
            CompleteActionAndEvaluateTransition();
        }
    }

    public override void Exit()
    {
        base.Exit();

        if (_playerAnimController.AnimatorInstance != null)
        {
            _playerAnimController.AnimatorInstance.speed = 1.0f;
        }

        _playerAnimController.SetRootMotion(false);
        CloseHitboxInternal();

        _playerAnimController.ResetTrigger(AnimParamType.Attack);
        _playerAnimController.SetInteger(AnimParamType.ComboStep, 0);
        owner.InputReader.ClearCommand(InputCommandType.Attack);

        _hasAdvancedThisStep = false;
        _isHitboxCurrentlyOpen = false;
        _hasOpenedHitboxThisStep = false;
        _hasEnteredCurrentAttackClip = false;
        _sawTransition = false;
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
        _sawTransition = false;
        _hasEnteredCurrentAttackClip = false;

        UnityEngine.Animator animator = _playerAnimController.AnimatorInstance;
        _previousStateHash = animator != null ? animator.GetCurrentAnimatorStateInfo(0).fullPathHash : 0;

        WeaponItemData weapon = owner.CombatModule.CurrentWeaponData;
        _currentTiming = (weapon != null) ? weapon.GetComboTiming(_comboStep) : new WeaponItemData.ComboTiming(0.35f, 0.55f, 0.30f, 0.65f, 0.80f);

        _currentAttackSpeedMult = 1.0f;
        if (_playerStatusController != null && _playerStatusController.PlayerInfoSource != null)
        {
            _currentAttackSpeedMult = _playerStatusController.PlayerInfoSource
                .CalculateAttackSpeedMultiplier(_playerStatusController.EquippedBonusDexterity);
        }

        if (animator != null)
        {
            animator.speed = _currentAttackSpeedMult;
        }

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
        if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(nextCost)) return;

        CloseHitboxInternal();
        _hasAdvancedThisStep = true;
        _stepTimer = 0f;

        _comboStep = (_comboStep >= MaxComboStep) ? 1 : _comboStep + 1;
        owner.InputReader.ClearCommand(InputCommandType.Attack);

        // 락온 중에는 타깃 정면을 재추적하고, 비락온 시에만 이동 입력 방향으로 재회전
        if (owner.CurrentLockOnTarget != null)
        {
            AlignAttackDirection();
        }
        else if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            ApplyImmediateRotation(UnityEngine.Quaternion.LookRotation(owner.CurrentMoveDirection));
        }

        ExecuteAttackStep();
    }

    private void UpdateHitboxWindow(float normalizedTime)
    {
        if (_hasAdvancedThisStep || !_hasEnteredCurrentAttackClip)
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

    private void ApplyImmediateRotation(UnityEngine.Quaternion targetRot)
    {
        owner.transform.rotation = targetRot;
        if (owner.Rigidbody != null && !owner.Rigidbody.isKinematic)
        {
            owner.Rigidbody.MoveRotation(targetRot);
        }
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
                ApplyImmediateRotation(UnityEngine.Quaternion.LookRotation(dirToLockOn.normalized));
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
                ApplyImmediateRotation(UnityEngine.Quaternion.LookRotation(targetDir.normalized));
                return;
            }
            return;
        }

        // 3. 탐색된 적이 없다면 현재 이동 입력 방향으로 회전
        if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            ApplyImmediateRotation(UnityEngine.Quaternion.LookRotation(owner.CurrentMoveDirection.normalized));
        }
    }
}