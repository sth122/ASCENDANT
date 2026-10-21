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
            float guardSpeed = owner.UnitStatusController.MotionStatus.WalkSpeed.Value * GuardMoveSpeedFactor;
            owner.MoveAPI(owner.CurrentMoveDirection, guardSpeed);
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
    /// 공격 수신 시 가드 또는 패링 분기 연산
    /// 수평면 투영(Dot Product)을 통해 높낮이 지형에 의한 판정 뚫림 완벽 차단
    /// </summary>
    public bool TryProcessBlockOrParry(in DamageContext context, out bool parrySuccess)
    {
        parrySuccess = false;

        if (context.Attacker != null)
        {
            UnityEngine.Vector3 dirToAttacker = context.Attacker.transform.position - owner.transform.position;
            dirToAttacker.y = 0f; // 수평 투영

            if (dirToAttacker.sqrMagnitude > 0.001f)
            {
                dirToAttacker.Normalize();
                float dot = UnityEngine.Vector3.Dot(owner.transform.forward, dirToAttacker);
                float minDot = UnityEngine.Mathf.Cos((GuardBlockAngle * 0.5f) * UnityEngine.Mathf.Deg2Rad);

                // 방어 허용 각도를 벗어난 측후면 피격은 방어 실패
                if (dot < minDot) return false;
            }
        }

        // 1. 패링 윈도우 적중 시 패링 카운터 발동
        if (IsParryWindow && context.IsParryable)
        {
            parrySuccess = true;
            IsParryWindow = false;
            TriggerParrySuccess(context.Attacker);
            return true;
        }

        // 2. 일반 가드 피격 시 스태미나 차감
        if (_playerStatusController != null)
        {
            bool isGuardBroken = _playerStatusController.BlockAttackWithStamina(context.Damage, ShieldStability);

            if (isGuardBroken)
            {
                _playerStateMachine.ChangeState(UnitState.Stun);
            }
            else
            {
                // 가드 사운드 및 히트 이팩트 처리
            }
        }
        return true;
    }

    private void TriggerParrySuccess(UnityEngine.GameObject attacker)
    {
        _playerAnimController.SetTrigger(AnimParamType.ParrySuccess);

        // 적 유닛 피격/경직 인터페이스 호출
        if (attacker != null)
        {
            EnemyController enemy = attacker.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                // 상대 그로기 유발
                enemy.OnParried();
            }
     
            // 패링 성공 연출
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

