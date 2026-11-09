public class PlayerFallState : PlayerBaseState
{
    private float _fallDuration = 0f;
    private const float HardLandingThreshold = 0.85f; // 0.85초 이상 체공 시 하드 랜딩 판정

    public PlayerFallState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        _fallDuration = 0f;
        // 체공 루프 애니메이션 전용 재생
        _playerAnimController.SetBool(AnimParamType.Fall, true);
    }

    public override void Exit() => _playerAnimController.SetBool(AnimParamType.Fall, false);

    public override void Update()
    {
        base.Update();
        _fallDuration += UnityEngine.Time.deltaTime;

        if (owner.Movement.CheckGrounded())
        {
            HandleLanding();
        }
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            float airSpeed = _playerStatusController.MotionStatus.WalkSpeed.Value * 0.35f;
            UnityEngine.Vector3 airVelocity = owner.CurrentMoveDirection * airSpeed;
            owner.Rigidbody.linearVelocity = new UnityEngine.Vector3(airVelocity.x, owner.Rigidbody.linearVelocity.y, airVelocity.z);
        }
    }

    private void HandleLanding()
    {
        owner.StopMovementAPI();

        if (_fallDuration >= HardLandingThreshold)
        {
            _playerStateMachine.ChangeState(UnitState.HardLanding);
            return;
        }

        // 일반 경착지 시 이동 입력에 따라 즉시 복귀
        if (owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
        {
            _playerStateMachine.ChangeState(owner.InputReader.IsSprinting ? UnitState.Sprint : UnitState.Move);
        }
        else
        {
            _playerStateMachine.ChangeState(UnitState.Idle);
        }
    }
}