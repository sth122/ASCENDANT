using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// C키 입력을 유지하는 동안 방패를 거치하고 Strafe 보행 및 가드 방어를 수행하며,
/// 진입 직후 초기 프레임 동안 패링 활성화를 통제하는 FSM 상태
/// </summary>
public class PlayerGuardState : PlayerActionState
{
    private bool _isParryActive = false;
    private GameObject _lastParriedAttacker = null;
    private Transform _guardTargetEnemy = null;
    public bool IsParryActive => _isParryActive;

    public PlayerGuardState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;
        _isParryActive = false;
        _lastParriedAttacker = null;
        owner.StopMovementAPI();

        // 1. 가장 가까운 적(또는 락온 대상)을 탐색하여 즉시 가드 방향 정렬
        _guardTargetEnemy = owner.FindClosestEnemy(15f, requireLineOfSight: true);
        if (_guardTargetEnemy != null)
        {
            owner.Movement.SnapToTarget(_guardTargetEnemy.position);
        }

        // 2. 방패 거치 애니메이션 파라미터 On (제자리 가드)
        _playerAnimController.SetFloat(AnimParamType.StrafeX, 0f);
        _playerAnimController.SetFloat(AnimParamType.StrafeZ, 0f);
        _playerAnimController.SetBool(AnimParamType.IsGuarding, true);

        // 3. 가드 중 스태미나 회복 지연 배율 적용
        float regenFactor = _playerCombatConfigSO != null ? _playerCombatConfigSO.GuardStaminaRegenFactor : 0.3f;
        _playerStatusController.StaminaRegenMultiplier = regenFactor;

        // 4. C키 해제 및 회피 이벤트 바인딩
        EnableGuardEvents();

        // 5. 방패 스펙에 따른 초기 패링 유효 윈도우 타이머 가동
        float windowDuration = GetParryWindowDuration();
        CancellationToken token = CreateActionToken();
        ExecuteParryWindowAsync(windowDuration, token).Forget();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // 가드 유지 중에는 이동 정지 유지
        owner.StopMovementAPI();
        _playerAnimController.SetFloat(AnimParamType.StrafeX, 0f);
        _playerAnimController.SetFloat(AnimParamType.StrafeZ, 0f);

        // 가드 중인 대상(또는 락온 대상)이 살아있다면 방패 방향만 대상 쪽으로 추적 회전
        Transform trackTarget = owner.CurrentLockOnTarget != null ? owner.CurrentLockOnTarget : _guardTargetEnemy;
        if (trackTarget != null)
        {
            UnitStatusController targetStat = trackTarget.GetComponentInParent<UnitStatusController>();
            if (targetStat != null && !targetStat.IsDead)
            {
                float rotSpeed = (_playerStatusController != null && _playerStatusController.PlayerInfoSource != null)
                    ? _playerStatusController.PlayerInfoSource.RotationSpeed : 16f;

                Vector3 targetDir = trackTarget.position - owner.transform.position;
                targetDir.y = 0f;
                if (targetDir.sqrMagnitude > 0.001f)
                {
                    owner.RotateTowardsAPI(targetDir.normalized, rotSpeed, Time.fixedDeltaTime);
                }
            }
            else
            {
                _guardTargetEnemy = owner.FindClosestEnemy(15f, requireLineOfSight: true);
            }
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
        _guardTargetEnemy = null;
    }

    /// <summary>
    /// 공격 피격 시 패링 성공, 일반 가드 임팩트, 가드 브레이크 분기 처리
    /// </summary>
    public BlockResultType ProcessIncomingAttack(in DamageContext context)
    {
        float blockAngle = _playerCombatConfigSO != null ? _playerCombatConfigSO.GuardBlockAngle : 120.0f;

        // 피격 시 공격자 방향으로 방패 정면 정렬 보정
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

        // 패링 불가 공격 검사
        if (!context.IsParryable)
        {
            owner.StateMachineInstance.ChangeState(UnitState.Knockback, forceReenter: true);
            return BlockResultType.Knockback;
        }

        // [패링 성공 분기]
        if (_isParryActive)
        {
            _isParryActive = false;
            _lastParriedAttacker = context.Attacker;

            if (context.ImpactType == AttackImpactType.Heavy)
            {
                _playerAnimController.SetTrigger(AnimParamType.GuardImpact);
                if (owner.Rigidbody != null)
                {
                    owner.Rigidbody.AddForce(-owner.transform.forward * 4.5f, ForceMode.Impulse);
                }
                return BlockResultType.HeavyParryPushback;
            }

            if (owner.StateMachineInstance.GetState(UnitState.Parry) is PlayerParryState parryState)
            {
                parryState.SetParriedAttacker(_lastParriedAttacker);
            }
            _lastParriedAttacker = null;

            owner.StateMachineInstance.ChangeState(UnitState.Parry);
            return BlockResultType.ParrySuccess;
        }

        // [일반 가드 분기]
        if (_playerStatusController != null)
        {
            float stability = (_playerStatusController.EquippedShieldStability > 0f)
                ? (_playerStatusController.EquippedShieldStability / 100.0f)
                : (_playerCombatConfigSO != null ? _playerCombatConfigSO.ShieldStability : 0.7f);

            float staminaCost = context.Damage * (1.0f - stability);
            float currentStamina = _playerStatusController.CurrentStamina;

            if (currentStamina < staminaCost)
            {
                float chipRatio = (staminaCost - currentStamina) / Mathf.Max(1.0f, staminaCost);
                _playerStatusController.ConsumeStamina(currentStamina);
                _playerStatusController.TakeDamage(context.Damage * chipRatio, context.PoiseDamage);

                owner.StateMachineInstance.ChangeState(UnitState.GuardBreak, forceReenter: true);
                return BlockResultType.GuardBreak;
            }

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