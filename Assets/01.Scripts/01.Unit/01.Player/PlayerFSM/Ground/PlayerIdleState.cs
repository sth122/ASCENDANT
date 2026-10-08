/// <summary>
/// Player의 동작 없음(입력 없음)을 전담하는 FSM 상태 클래스
/// </summary>
public class PlayerIdleState : PlayerGroundState
{
    public PlayerIdleState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        // 이전 상태 잔여 관성 정지
        owner.Movement.StopMovement();
    }

    public override void Update()
    {
        base.Update();

        // 이동 입력이 감지되면 Shift 여부에 따라 상태 전이
        if(owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
        {
            if(owner.InputReader.IsSprinting)
            {
                playerStateMachine.ChangeState(UnitState.Sprint);
            }
            else
            {
                playerStateMachine.ChangeState(UnitState.Move);
            }
        }
    }
}
