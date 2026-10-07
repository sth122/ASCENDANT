
public class PlayerIdleState : UnitIdleState<PlayerController>
{
    private readonly PlayerStateMachine playerStateMachine;

    public PlayerIdleState(PlayerController owner, PlayerStateMachine stateMachine) 
        : base(owner, stateMachine)
    {
        this.playerStateMachine = stateMachine;
    }

    public override void Enter()
    {
        base.Enter();

        // 이전 상태 잔여 관성 정지
        owner.Movement.StopMovement();
    }

    public override void Update()
    {
        base.Update();

        // 1. 구르기 입력 우선 체크
        if(owner.InputReader.ConsumeRollInput())
        {
            playerStateMachine.ChangeState(UnitState.Roll);
            return;
        }

        // 2. 점프 입력 체크
        if(owner.Movement.IsGrounded && owner.InputReader.ConsumeJumpInput())
        {
            playerStateMachine.ChangeState(UnitState.Jump);
            return;
        }

        // 3. 이동 입력이 감지되면 Shift 여부에 따라 상태 전이
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
    public override void FixedUpdate()
    {
        base.FixedUpdate();
        // Idle 상태에서의 공통된 물리적 업데이트를 처리
    }

    public override void Exit()
    {
        base.Exit();
    }
}
