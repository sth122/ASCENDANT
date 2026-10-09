// 모든 유닛이 공유할 수 있는 제네릭 Idle 상태 클래스
public class PlayerGroundState : UnitBaseState<PlayerController>
{
    protected readonly PlayerStateMachine _playerStateMachine;

    public PlayerGroundState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine)
    {
        this._playerStateMachine = stateMachine;
    }
    public override void Enter()
    {
        base.Enter();
        // 상태 입장 시 커맨드 이벤트 구독
        owner.InputReader.OnCommandInputEvent -= HandleCommand;
        owner.InputReader.OnMoveInputChanged -= HandleMoveInputChanged;
        owner.InputReader.OnSprintChanged -= HandleSprintChanged;

        owner.InputReader.OnCommandInputEvent += HandleCommand;
        owner.InputReader.OnMoveInputChanged += HandleMoveInputChanged;
        owner.InputReader.OnSprintChanged += HandleSprintChanged;
    }

    public override void Exit()
    {
        base.Exit();
        // 상태 탈출 시 커맨드 이벤트 구독 해제
        owner.InputReader.OnCommandInputEvent -= HandleCommand;
        owner.InputReader.OnMoveInputChanged -= HandleMoveInputChanged;
        owner.InputReader.OnSprintChanged -= HandleSprintChanged;
    }

    /// <summary>
    /// 키 입력이 들어오는 순간 호출되는 이벤트 콜백 메서드
    /// </summary>
    /// <param name="command"></param>
    public virtual void HandleCommand(InputCommandType command)
    {
        switch (command)
        {
            case InputCommandType.Roll:
                _playerStateMachine.ChangeState(UnitState.Roll); 
                break;

            case InputCommandType.Jump:
                if (owner.Movement.IsGrounded)
                {
                    _playerStateMachine.ChangeState(UnitState.Jump);
                }
                break;

            case InputCommandType.Attack:
                _playerStateMachine.ChangeState(UnitState.Attack);
                break;

            case InputCommandType.Parry:
                _playerStateMachine.ChangeState(UnitState.Parry); 
                break;
        }
    }

    protected virtual void HandleMoveInputChanged(UnityEngine.Vector2 moveInput) { }
    protected virtual void HandleSprintChanged(bool isSprinting) { }
}
