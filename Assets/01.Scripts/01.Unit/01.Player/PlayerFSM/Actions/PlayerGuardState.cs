using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// C키 입력을 유지하는 동안 방패를 거치하고 Strafe 보행 및 가드 방어를 수행하며,
/// 진입 직후 초기 프레임 동안 패링 활성화를 통제하는 FSM 상태
/// </summary>
public class PlayerGuardState : PlayerActionState
{
    private bool _isParryActive = false;
    private GameObject _lastParriedAttacker = null;

    public bool IsParryActive => _isParryActive;

    public PlayerGuardState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        CanMove = true;
        CanRotate = true;
        _isParryActive = false;
        _lastParriedAttacker = null;

        // 1. 방패 거치 애니메이션 파라미터 On
        _playerAnimController.SetBool(AnimParamType.IsGuarding, true);

        // 2. 가드 중 스태미나 회복 지연 배율 적용
        float regenFactor = _playerCombatConfigSO != null ? _playerCombatConfigSO.GuardStaminaRegenFactor : 0.3f;
        _playerStatusController.StaminaRegenMultiplier = regenFactor;

        // 3. C키 해제 및 회피 이벤트 바인딩
        EnableGuardEvents();

        // 4. 방패 스펙에 따른 초기 패링 유효 윈도우 타이머 가동
        float windowDuration = GetParryWindowDuration();
        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteParryWindowAsync(windowDuration, token).Forget();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        float speedFactor = _playerCombatConfigSO != null ? _playerCombatConfigSO.GuardMoveSpeedFactor : 0.75f;

        // 4방향 Strafe 애니메이션 파라미터 주입
        Vector3 localMove = owner.transform.InverseTransformDirection(owner.CurrentMoveDirection);
        _playerAnimController.SetFloat(AnimParamType.StrafeX, localMove.x);
        _playerAnimController.SetFloat(AnimParamType.StrafeZ, localMove.z);

        if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            float guardSpeed = owner.StatusController.MotionStatus.WalkSpeed.Value * speedFactor;
            owner.MoveAPI(owner.CurrentMoveDirection, guardSpeed);

            float rotSpeed = (_playerStatusController != null && _playerStatusController.PlayerInfoSource != null)
                ? _playerStatusController.PlayerInfoSource.RotationSpeed : 16f;

            if (owner.CurrentLockOnTarget != null)
            {
                Vector3 targetDir = owner.CurrentLockOnTarget.position - owner.transform.position;
                targetDir.y = 0f;
                if (targetDir.sqrMagnitude > 0.001f)
                {
                    owner.RotateTowardsAPI(targetDir.normalized, rotSpeed, Time.fixedDeltaTime);
                }
            }
        }
        else
        {
            owner.StopMovementAPI();
        }
    }

    public override void Exit()
    {
        base.Exit();

        _playerAnimController.SetBool(AnimParamType.IsGuarding, false);
        _playerAnimController.SetFloat(AnimParamType.StrafeX, 0f);
        _playerAnimController.SetFloat(AnimParamType.StrafeZ, 0f);
        _playerStatusController.StaminaRegenMultiplier = 1.0f;

        DisableGuardEvents();
        _isParryActive = false;
        _lastParriedAttacker = null;
    }

    /// <summary>
    /// 공격 피격 시 패링 성공, 일반 가드 임팩트, 가드 브레이크 분기 처리
    /// </summary>
    public BlockResultType ProcessIncomingAttack(in DamageContext context)
    {
        float blockAngle = _playerCombatConfigSO != null ? _playerCombatConfigSO.GuardBlockAngle : 120.0f;

        // 1. 방어 각도 검사 (전방 180도)
        if (context.Attacker != null)
        {
            Vector3 dirToAttacker = context.Attacker.transform.position - owner.transform.position;
            dirToAttacker.y = 0f;

            if (dirToAttacker.sqrMagnitude > 0.001f)
            {
                float dot = Vector3.Dot(owner.transform.forward, dirToAttacker.normalized);
                float minDot = Mathf.Cos((blockAngle * 0.5f) * Mathf.Deg2Rad);
                if (dot < minDot) return BlockResultType.Failed;
            }
        }

        // 2. 패링 불가 공격 검사
        if (!context.IsParryable)
        {
            owner.StateMachineInstance.ChangeState(UnitState.Knockback, forceReenter: true);
            return BlockResultType.Knockback;
        }

        // 3. [패링 성공 분기] 초기 패링 윈도우가 살아있을 때 피격된 경우
        if (_isParryActive)
        {
            _isParryActive = false;
            _lastParriedAttacker = context.Attacker;

            // 강공격(Heavy) 패링 시 밀림 연출
            if (context.ImpactType == AttackImpactType.Heavy)
            {
                // 강공격 패링 전용 애니메이션이 없으므로 일반 가드 임팩트 모션을 재생
                // 본래라면 ParryState에서 따로 처리하는 것이 맞음
                _playerAnimController.SetTrigger(AnimParamType.GuardImpact);
                if (owner.Rigidbody != null)
                {
                    owner.Rigidbody.AddForce(-owner.transform.forward * 4.5f, ForceMode.Impulse);
                }
                return BlockResultType.HeavyParryPushback;
            }

            // 일반 공격 패링 성공 -> PlayerParryState(반격 모션 FSM)로 전이
            if (owner.StateMachineInstance.GetState(UnitState.Parry) is PlayerParryState parryState)
            {
                parryState.SetParriedAttacker(_lastParriedAttacker);
            }
            _lastParriedAttacker = null;

            owner.StateMachineInstance.ChangeState(UnitState.Parry);
            return BlockResultType.ParrySuccess;
        }

        // 4. [일반 가드 분기] 패링 타이밍이 지났으나 C키를 유지하며 방어한 경우
        if (_playerStatusController != null)
        {
            float stability = (_playerStatusController.EquippedShieldStability > 0f)
                ? (_playerStatusController.EquippedShieldStability / 100.0f)
                : (_playerCombatConfigSO != null ? _playerCombatConfigSO.ShieldStability : 0.7f);

            float staminaCost = context.Damage * (1.0f - stability);
            float currentStamina = _playerStatusController.CurrentStamina;

            // 가드 브레이크 판정
            if (currentStamina < staminaCost)
            {
                float chipRatio = (staminaCost - currentStamina) / Mathf.Max(1.0f, staminaCost);
                _playerStatusController.ConsumeStamina(currentStamina);
                _playerStatusController.TakeDamage(context.Damage * chipRatio, context.PoiseDamage);

                owner.StateMachineInstance.ChangeState(UnitState.GuardBreak, forceReenter: true);
                return BlockResultType.GuardBreak;
            }

            // 방패 커트율 적용 대미지 차감 및 스태미나 소비
            float cutRatio = _playerStatusController.EquippedShieldAbsorption;
            float chipDamage = context.Damage * (1.0f - cutRatio);
            if (chipDamage > 0f)
            {
                _playerStatusController.TakeDamage(chipDamage, 0f);
            }

            _playerStatusController.ConsumeStamina(staminaCost);
            _playerAnimController.SetTrigger(AnimParamType.GuardImpact);

            if (owner.Rigidbody != null)
            {
                owner.Rigidbody.AddForce(-owner.transform.forward * 1.5f, ForceMode.Impulse);
            }

            return BlockResultType.GuardImpact;
        }

        return BlockResultType.Failed;
    }

    public GameObject GetAndClearParriedAttacker()
    {
        GameObject attacker = _lastParriedAttacker;
        _lastParriedAttacker = null;
        return attacker;
    }

    private float GetParryWindowDuration()
    {
        EquipmentItem shieldItem = null;
        if (owner.TryGetComponent(out PlayerInventoryHandler handler) && handler.InventoryInstance != null)
        {
            shieldItem = handler.InventoryInstance.GetEquippedItem(EquipSlotType.Shield);
        }

        if (shieldItem is ShieldItem shield && shield.ShieldData != null)
        {
            return shield.ShieldData.ParryWindowDuration;
        }

        return _playerCombatConfigSO != null ? _playerCombatConfigSO.ParryWindowDuration : 0.15f;
    }

    private async UniTaskVoid ExecuteParryWindowAsync(float windowDuration, System.Threading.CancellationToken token)
    {
        _isParryActive = true;
        bool canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(windowDuration), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        _isParryActive = false; // 패링 유효 시간 종료 -> C키를 누르고 있는 동안 일반 가드로 유지
    }

    #region 이벤트 바인딩
    private void EnableGuardEvents()
    {
        DisableGuardEvents();
        if (owner.InputReader != null)
        {
            owner.InputReader.OnCommandInputEvent += HandleCommandDuringGuard;
            owner.InputReader.OnGuardReleased += HandleGuardReleased;
        }
    }

    private void DisableGuardEvents()
    {
        if (owner.InputReader != null)
        {
            owner.InputReader.OnCommandInputEvent -= HandleCommandDuringGuard;
            owner.InputReader.OnGuardReleased -= HandleGuardReleased;
        }
    }

    private void HandleCommandDuringGuard(InputCommandType command)
    {
        if (command == InputCommandType.Roll)
        {
            float rollCost = owner.CombatConfig != null ? owner.CombatConfig.RollStaminaCost : 18.0f;
            if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(rollCost))
            {
                owner.InputReader.ClearCommand(InputCommandType.Roll);
                return;
            }
            _playerStateMachine.ChangeState(UnitState.Roll);
        }
    }

    private void HandleGuardReleased()
    {
        // C키에서 손을 떼면 이동 입력 상태에 따라 복귀
        if (owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
        {
            _playerStateMachine.ChangeState(owner.InputReader.IsSprinting ? UnitState.Sprint : UnitState.Move);
        }
        else
        {
            _playerStateMachine.ChangeState(UnitState.Idle);
        }
    }
    #endregion
}