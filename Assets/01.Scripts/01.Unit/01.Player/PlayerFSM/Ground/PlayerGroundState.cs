// 모든 유닛이 공유할 수 있는 제네릭 Idle 상태 클래스
public class PlayerGroundState : PlayerBaseState
{
    private const float RequiredAttackStamina = 14.0f;

    public PlayerGroundState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }
    public override void Enter()
    {
        base.Enter();
        // 상태 입장 시 커맨드 이벤트 구독
        SubscribeEvent();
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
        switch (command)
        {
            case InputCommandType.Roll:
                // 스태미나가 부족하면 구르기 상태로 전이하지 않고 입력을 폐기
                if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(PlayerRollState.RollStaminaCost))
                {
                    owner.InputReader.ClearCommand(InputCommandType.Roll);
                    return;
                }
                _playerStateMachine.ChangeState(UnitState.Roll); 
                break;

            case InputCommandType.Jump:
                // 스태미나가 부족하면 점프 상태로 전이하지 않고 입력을 폐기
                if (!owner.Movement.IsGrounded)
                {
                    owner.InputReader.ClearCommand(InputCommandType.Jump);
                    return;
                }

                if (owner.Movement.IsGrounded)
                {
                    _playerStateMachine.ChangeState(UnitState.Jump);
                }
                break;

            case InputCommandType.Attack:
                // 1. 앞잡(Riposte) 대상이 있는지 우선 검사
                if (owner.TryTriggerRiposte())
                {
                    owner.InputReader.ClearCommand(InputCommandType.Attack);
                    return;
                }

                // 2. 스태미나가 부족하면 공격 상태로 전이하지 않고 입력을 폐기
                if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(RequiredAttackStamina))
                {
                    owner.InputReader.ClearCommand(InputCommandType.Attack);
                    return;
                }

                // 3. 자원이 충분할 때만 공격 FSM 진입
                _playerStateMachine.ChangeState(UnitState.Attack);
                break;

            case InputCommandType.Parry:
                _playerStateMachine.ChangeState(UnitState.Parry); 
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
        if (isSprinting && _playerStatusController != null && _playerStatusController.HasEnoughStamina(5.0f))
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
