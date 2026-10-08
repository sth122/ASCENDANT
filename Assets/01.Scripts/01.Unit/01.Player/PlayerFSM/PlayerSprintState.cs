using UnityEngine;

public class PlayerSprintState : PlayerMoveState
{
    public PlayerSprintState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Update()
    {
        // 1. 구르기/점프 우선 체크
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

        // 3. 이동 입력 중단 시 Idle 복귀
        if (owner.InputReader.MoveInput.sqrMagnitude <= 0.01f)
        {
            playerStateMachine.ChangeState(UnitState.Idle);
            return;
        }

        // 4. Sprint 키를 떼면 Move로 강등
        if (!owner.InputReader.IsSprinting)
        {
            playerStateMachine.ChangeState(UnitState.Move);
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
            owner.Movement.RotateTowards(moveDir, owner.RuntimePlayerStat.RotationSpeed, UnityEngine.Time.fixedDeltaTime);
        }
    }
}
