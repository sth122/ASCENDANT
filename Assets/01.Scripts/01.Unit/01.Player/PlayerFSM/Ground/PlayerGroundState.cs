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
                _playerStateMachine.ChangeState(UnitState.Roll); 
                break;

            case InputCommandType.Jump:
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
                if (_playerStatController != null && !_playerStatController.HasEnoughStamina(RequiredAttackStamina))
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

    protected virtual void HandleMoveInputChanged(UnityEngine.Vector2 moveInput) { }
    protected virtual void HandleSprintChanged(bool isSprinting) { }

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
