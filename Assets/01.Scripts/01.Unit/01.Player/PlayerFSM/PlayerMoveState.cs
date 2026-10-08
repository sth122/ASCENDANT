/// <summary>
/// Player의 움직임을 전담하는 FSM 상태 클래스
/// </summary>
public class PlayerMoveState : UnitMoveState<PlayerController>
{
    protected readonly PlayerStateMachine playerStateMachine;
    public PlayerMoveState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine)
    {
        this.playerStateMachine = stateMachine;
    }

    public override void Enter()
    {
        base.Enter();
        // 공통적인 물리적 상태(이동, 관성 등)를 초기화하거나, 애니메이션을 재생하는 등의 작업을 수행
    }
    public override void Update()
    {
        base.Update();

        // 1. 회피/구르기 입력 우선 체크
        if (owner.InputReader.ConsumeRollInput())
        {
            playerStateMachine.ChangeState(UnitState.Roll);
            return;
        }

        // 2. 점프 입력 체크
        if (owner.Movement.IsGrounded && owner.InputReader.ConsumeJumpInput())
        {
            playerStateMachine.ChangeState(UnitState.Jump);
            return;
        }

        // 3. 이동 입력이 중단되면 Idle로 복귀
        if (owner.InputReader.MoveInput.sqrMagnitude <= 0.01f)
        {
            playerStateMachine.ChangeState(UnitState.Idle);
            return;
        }

        // 4. Shift 입력 유지 시 Sprint로 전이
        if (owner.InputReader.IsSprinting)
        {
            playerStateMachine.ChangeState(UnitState.Sprint);
            return;
        }
    }
    public override void FixedUpdate()
    {
        base.FixedUpdate();

        float walkSpeed = owner.StatController.MotionStats.WalkSpeed.Value;
        UnityEngine.Vector3 moveDir = owner.CurrentMoveDirection;

        // 물리 이동
        owner.Movement.Move(moveDir, walkSpeed);

        if(owner.CurrentLockOnTarget == null && moveDir.sqrMagnitude > 0.001f)
        {
            owner.Movement.RotateTowards(moveDir, owner.RuntimePlayerStat.RotationSpeed, UnityEngine.Time.fixedDeltaTime);
        }
    }

    public override void Exit()
    {
        base.Exit();
    }
}
