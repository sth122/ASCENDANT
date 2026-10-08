/// <summary>
/// Player 달리기 이동 상태를 전담하는 Player FSM 상태 클래스
/// </summary>
public class PlayerSprintState : PlayerMoveState
{
    public PlayerSprintState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Update()
    {
        if (owner.InputReader.MoveInput.sqrMagnitude <= 0.01f)
        {
            _playerStateMachine.ChangeState(UnitState.Idle);
            return;
        }

        if (!owner.InputReader.IsSprinting)
        {
            _playerStateMachine.ChangeState(UnitState.Move);
            return;
        }
    }

    public override void FixedUpdate()
    {
        float sprintSpeed = owner.StatController.MotionStats.SprintSpeed.Value;
        UnityEngine.Vector3 moveDir = owner.CurrentMoveDirection;

        // 물리 이동
        owner.Movement.Move(moveDir, sprintSpeed);

        if (owner.CurrentLockOnTarget == null && moveDir.sqrMagnitude > 0.001f)
        {
            owner.Movement.RotateTowards(moveDir, owner.GetPlayerStat().RotationSpeed, UnityEngine.Time.fixedDeltaTime);
        }
    }
}
