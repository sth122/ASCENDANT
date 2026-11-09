// 모든 유닛이 공유할 수 있는 제네릭 Idle 상태 클래스
public class PlayerGroundState : PlayerBaseState
{
    public PlayerGroundState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }
    public override void Enter()
    {
        base.Enter();
        // 상태 입장 시 커맨드 이벤트 구독
        SubscribeEvent();
    }

    public override void Update()
    {
        base.Update();
        if (!owner.Movement.CheckGrounded() && owner.Rigidbody.linearVelocity.y < -1.5f)
        {
            _playerStateMachine.ChangeState(UnitState.Fall);
            return;
        }
    }

    public override void Exit()
    {
        base.Exit();
        // 상태 탈출 시 커맨드 이벤트 구독 해제
        UnsubscribeEvent();
    }

    /// <summary>
    /// 키 입력이 들어오는 순간 호출되는 이벤트 콜백 메서드
    /// </summary>
    /// <param name="command"></param>
    public virtual void HandleCommandInput(InputCommandType command)
    {
        PlayerCombatConfigSO config = owner.CombatConfig;

        switch (command)
        {
            case InputCommandType.Roll:
                float rollCost = config != null ? config.RollStaminaCost : 18.0f;
                if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(rollCost))
                {
                    owner.InputReader.ClearCommand(InputCommandType.Roll);
                    return;
                }
                _playerStateMachine.ChangeState(UnitState.Roll);
                break;

            case InputCommandType.Jump:
                if (!owner.Movement.IsGrounded)
                {
                    owner.InputReader.ClearCommand(InputCommandType.Jump);
                    return;
                }

                float jumpCost = config != null ? config.JumpStaminaCost : 12.0f;
                if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(jumpCost))
                {
                    owner.InputReader.ClearCommand(InputCommandType.Jump);
                    return;
                }

                _playerStateMachine.ChangeState(UnitState.Jump);
                break;

            case InputCommandType.Attack:
                if (owner.TryTriggerRiposte())
                {
                    owner.InputReader.ClearCommand(InputCommandType.Attack);
                    return;
                }

                float atkCost = config != null ? config.AttackStaminaCost : 14.0f;
                if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(atkCost))
                {
                    owner.InputReader.ClearCommand(InputCommandType.Attack);
                    return;
                }

                _playerStateMachine.ChangeState(UnitState.Attack);
                break;

            case InputCommandType.Guard:
                _playerStateMachine.ChangeState(UnitState.Guard);
                break;
        }
    }

    protected virtual void HandleMoveInputChanged(UnityEngine.Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude <= 0.01f)
        {
            _playerStateMachine.ChangeState(UnitState.Idle);
        }
    }
    protected virtual void HandleSprintChanged(bool isSprinting)
    {
        // Shift 입력 감지 시 스태미나 여유가 있으면 Sprint 상태로 전이
        float minStamina = owner.CombatConfig != null ? owner.CombatConfig.MinimumSprintStamina : 5.0f;
        if (isSprinting && _playerStatusController != null && _playerStatusController.HasEnoughStamina(minStamina))
        {
            _playerStateMachine.ChangeState(UnitState.Sprint);
        }
        else if (!isSprinting && _playerStateMachine.CurrentState is PlayerSprintState)
        {
            _playerStateMachine.ChangeState(UnitState.Move);
        }
    }

    private void SubscribeEvent()
    {
        UnsubscribeEvent();
        owner.InputReader.OnCommandInputEvent += HandleCommandInput;
        owner.InputReader.OnMoveInputChanged += HandleMoveInputChanged;
        owner.InputReader.OnSprintChanged += HandleSprintChanged;
    }
    private void UnsubscribeEvent()
    {
        owner.InputReader.OnCommandInputEvent -= HandleCommandInput;
        owner.InputReader.OnMoveInputChanged -= HandleMoveInputChanged;
        owner.InputReader.OnSprintChanged -= HandleSprintChanged;
    }
}
