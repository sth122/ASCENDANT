using UnityEngine;

/// <summary>
/// Player 방패 가드 유지 및 패링을 전담하는 Player FSM 액션 상태 클래스
/// 가드를 올린 직후 짧은 프레임 동안 패링 판정을 활성화하고,
/// 가드 유지 중에는 이동 속도 및 스태미나 회복 속도 감소
/// </summary>
public class PlayerParryState : PlayerActionState
{
    #region MyRegion
    private const float ParryWindowDuration = 0.2f;             // 패링 유효 시간
    private const float GuardMoveSpeedFactor = 0.75f;            // 가드 중 걷기 속도 배율
    private const float GuardStaminaRegenFactor = 0.3f;   // 가드 중 스태미나 회복 배율
    private const float ShieldStability = 0.7f;           // 기본 방패 버티기(감쇄력) 수치
    private const float GuardBlockAngle = 120.0f;
    #endregion

    public bool IsParryWindow { get; private set; }

    public PlayerParryState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }


    public override void Enter()
    {
        base.Enter();

        // 1. 가드 상태는 저속 이동과 방향 전환 가능
        CanMove = true;
        CanRotate = true;

        _playerAnimController.SetBool(AnimParamType.IsGuarding, true);
        _playerAnimController.SetTrigger(AnimParamType.Parry);

        _playerStatusController.StaminaRegenMultiplier = GuardStaminaRegenFactor;

        EnableEventHandler();

        // 3. 패링 판정 타이머
        // 부모 토큰을 사용하여 패링 유효 윈도우 비동기 카운트다운 시작
        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteParryWindowAsync(token).Forget();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // 2. 가드 보행 이동 및 Strafe 파라미터 전달
        UnityEngine.Vector2 rawInput = owner.InputReader.MoveInput;
        _playerAnimController.SetFloat(AnimParamType.StrafeX, rawInput.x);
        _playerAnimController.SetFloat(AnimParamType.StrafeZ, rawInput.y);

        if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            float guardSpeed = owner.StatusController.MotionStatus.WalkSpeed.Value * GuardMoveSpeedFactor;
            owner.MoveAPI(owner.CurrentMoveDirection, guardSpeed);

            float rotSpeed = (_playerStatusController != null && _playerStatusController.PlayerInfoSource != null)
                ? _playerStatusController.PlayerInfoSource.RotationSpeed : 16f;

            if (owner.CurrentLockOnTarget != null)
            {
                UnityEngine.Vector3 targetDir = owner.CurrentLockOnTarget.position - owner.transform.position;
                targetDir.y = 0f;
                if (targetDir.sqrMagnitude > 0.001f)
                {
                    owner.RotateTowardsAPI(targetDir.normalized, rotSpeed, UnityEngine.Time.fixedDeltaTime);
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
        _playerAnimController.ResetTrigger(AnimParamType.Parry);
        _playerAnimController.SetFloat(AnimParamType.StrafeX, 0f);
        _playerAnimController.SetFloat(AnimParamType.StrafeZ, 0f);
        _playerStatusController.StaminaRegenMultiplier = 1.0f;

        DisableEventHandler();

        IsParryWindow = false;
    }

    /// <summary>
    /// 공격 수신 시 패링 성공, 가드 유지 임팩트, 넉백, 가드파쇄 분기 처리
    /// </summary>
    public BlockResultType ProcessIncomingAttack(in DamageContext context)
    {
        // 1. 방어 각도 검사 (전방 120도)
        if (context.Attacker != null)
        {
            Vector3 dirToAttacker = context.Attacker.transform.position - owner.transform.position;
            dirToAttacker.y = 0f;

            if (dirToAttacker.sqrMagnitude > 0.001f)
            {
                float dot = Vector3.Dot(owner.transform.forward, dirToAttacker.normalized);
                float minDot = Mathf.Cos((GuardBlockAngle * 0.5f) * Mathf.Deg2Rad);
                if (dot < minDot) return BlockResultType.Failed; // 등 뒤 피격은 방어 불가
            }
        }

        // 2. 패링 불가 공격을 패링/가드하려 한 경우 -> Knockback(넉다운) 강제 전이
        if (!context.IsParryable)
        {
            owner.StateMachineInstance.ChangeState(UnitState.Knockback, forceReenter: true);
            return BlockResultType.Knockback;
        }

        // 3. 패링 유효 윈도우 중 피격
        if (IsParryWindow)
        {
            IsParryWindow = false;

            // 보스 강공격(Heavy) 패링 시: 적은 그로기에 걸리지 않고 플레이어가 역임펄스로 밀려남
            if (context.ImpactType == AttackImpactType.Heavy)
            {
                _playerAnimController.SetTrigger(AnimParamType.GetHit);
                if (owner.Rigidbody != null)
                {
                    owner.Rigidbody.AddForce(-owner.transform.forward * 4.5f, ForceMode.Impulse);
                }
                return BlockResultType.HeavyParryPushback;
            }

            // 일반 약공격(Light) 패링 성공: 적 그로기 및 카운터 모션 실행
            TriggerParrySuccess(context.Attacker);
            return BlockResultType.ParrySuccess;
        }

        // 4. 패링 타이밍이 빗나갔으나 가드를 유지하고 있는 상태 (일반 방어 판정)
        if (_playerStatusController != null)
        {
            float staminaCost = context.Damage * (1.0f - ShieldStability);
            float currentStamina = _playerStatusController.CurrentStamina;

            // 스태미나 고갈 시 -> 가드 브레이크(Stun)
            if (currentStamina < staminaCost)
            {
                float chipRatio = (staminaCost - currentStamina) / Mathf.Max(1.0f, staminaCost);
                _playerStatusController.ConsumeStamina(currentStamina);
                _playerStatusController.TakeDamage(context.Damage * chipRatio, context.PoiseDamage);

                owner.StateMachineInstance.ChangeState(UnitState.Stun, forceReenter: true);
                return BlockResultType.GuardBreak;
            }

            // 방어 성공: 스태미나 감산 후 방패 충격(Impact) 모션 재생
            _playerStatusController.ConsumeStamina(staminaCost);
            _playerAnimController.SetTrigger(AnimParamType.GuardImpact);

            // 미세 넉백 반동
            if (owner.Rigidbody != null)
            {
                owner.Rigidbody.AddForce(-owner.transform.forward * 1.5f, ForceMode.Impulse);
            }

            return BlockResultType.GuardImpact;
        }

        return BlockResultType.Failed;
    }

    private void TriggerParrySuccess(UnityEngine.GameObject attacker)
    {
        _playerAnimController.SetTrigger(AnimParamType.ParrySuccess);

        if (attacker != null)
        {
            EnemyBTController enemy = attacker.GetComponentInParent<EnemyBTController>();
            if (enemy != null)
            {
                enemy.OnParried();
            }
        }
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid ExecuteParryWindowAsync(System.Threading.CancellationToken token)
    {
        IsParryWindow = true;

        bool canceled = await Cysharp.Threading.Tasks.UniTask.Delay((int)(ParryWindowDuration * 1000f),
            cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        // 패링 타이밍 종료. 이후 유지 시 일반 가드로만 유지
        IsParryWindow = false;
    }

    #region EventHandler Func
    private void EnableEventHandler()
    {
        DisableEventHandler();
        owner.InputReader.OnCommandInputEvent += HandleCommandDuringGuard;
        owner.InputReader.OnGuardReleased += HandleGuardReleased;
    }
    private void DisableEventHandler()
    {
        owner.InputReader.OnCommandInputEvent -= HandleCommandDuringGuard;
        owner.InputReader.OnGuardReleased -= HandleGuardReleased;
    }
    private void HandleCommandDuringGuard(InputCommandType command)
    {
        // 가드 중 위급 상황 시 즉시 가드를 풀고 회피 전이
        if (command == InputCommandType.Roll)
        {
            // 스태미나 사전 검증
            if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(PlayerRollState.RollStaminaCost))
            {
                owner.InputReader.ClearCommand(InputCommandType.Roll);
                return;
            }
            _playerStateMachine.ChangeState(UnitState.Roll);
        }
    }

    private void HandleGuardReleased()
    {
        // 방패 버튼을 떼면 즉시 지상 기본 상태로 복귀
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