/// <summary>
/// Player의 움직임을 전담하는 FSM 상태 클래스
/// </summary>
public class PlayerMoveState : PlayerGroundState
{
    public PlayerMoveState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine)  {   }

    public override void Enter()
    {
        base.Enter();
        // 공통적인 물리적 상태(이동, 관성 등)를 초기화하거나, 애니메이션을 재생하는 등의 작업을 수행
    }
    public override void Update()
    {
        base.Update();

        if (owner.InputReader.MoveInput.sqrMagnitude <= 0.01f)
        {
            playerStateMachine.ChangeState(UnitState.Idle);
            return;
        }

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
            owner.Movement.RotateTowards(moveDir, owner.GetPlayerStat().RotationSpeed, UnityEngine.Time.fixedDeltaTime);
        }
    }

    public override void Exit()
    {
        base.Exit();
    }
}
