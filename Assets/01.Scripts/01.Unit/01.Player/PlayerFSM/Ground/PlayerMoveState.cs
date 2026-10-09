/// <summary>
/// Player의 걷기 이동 상태를 전담하는 FSM 상태 클래스
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
    public override void Update() => base.Update();

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        float walkSpeed = owner.StatController.MotionStats.WalkSpeed.Value;
        UnityEngine.Vector3 moveDir = owner.CurrentMoveDirection;

        // 물리 이동
        owner.MoveAPI(moveDir, walkSpeed);

        if(owner.CurrentLockOnTarget == null && moveDir.sqrMagnitude > 0.001f)
        {
            owner.RotateTowardsAPI(moveDir, owner.GetPlayerStat().RotationSpeed, UnityEngine.Time.fixedDeltaTime);
        }
    }

    protected override void HandleMoveInputChanged(UnityEngine.Vector2 moveInput)
    {
        if(moveInput.sqrMagnitude <= 0.01f)
        {
            _playerStateMachine.ChangeState(UnitState.Idle);
        }
    }

    protected override void HandleSprintChanged(bool isSprinting)
    {
        if (isSprinting)
        {
            _playerStateMachine.ChangeState(UnitState.Sprint);
        }
    }
}
