/// <summary>
/// Player 점프 동작을 전담하는 Player FSM 상태 클래스
/// 진입 시 수직 임펄스(AddForce)를 가하고 스태미나를 차감하며,
/// 하강 중 지면 감지 시점에 도달하면 착지 후 조건에 맞는 상태로 복귀
/// </summary>
public class PlayerJumpState : UnitBaseState<PlayerController>
{
    private readonly PlayerStateMachine _playerStateMachine;
    private readonly PlayerStatController _playerStatController;
   
    private const float JumpStaminaCost = 12.0f;
    private const float AirControlFactor = 0.4f;    // 공중 이동 제어 감쇠율
    private const float JumpCooldown = 0.15f;       // 도약 직후 즉시 찾지 판정도니는 것 방지
    private float _tiemInAir;

    public PlayerJumpState(PlayerController owner, PlayerStateMachine stateMachine) 
        : base(owner, stateMachine)
    {
        _playerStateMachine = stateMachine;
        _playerStatController = owner.PlayerStatController;
    }

    public override void Enter()
    {
        base.Enter();

        _tiemInAir = 0f;
        owner.ClearCommand(InputCommandType.Jump);

        // 1. 스태미나 차감
        _playerStatController?.ConsumeStamina(JumpStaminaCost);
        // 2. JumpForce 반영
        float jumpForce = _playerStatController != null ? 
            _playerStatController.MotionStats.JumpForce.Value : owner.StatController.MotionStats.RollForce.Value;

        owner.Movement.ApplyJump(jumpForce);
    }

    public override void Update()
    {
        base.Update();
        _tiemInAir += UnityEngine.Time.deltaTime;

        if(_tiemInAir > JumpCooldown && owner.Movement.IsGrounded)
        {
            owner.ClearCommand(InputCommandType.Jump);

            if(owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
            {
                if (owner.InputReader.IsSprinting)
                    _playerStateMachine.ChangeState(UnitState.Sprint);
                else
                    _playerStateMachine.ChangeState(UnitState.Move);
            }
            else
            {
                _playerStateMachine.ChangeState(UnitState.Idle);
            }
        }
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        if(owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            float airSpeed = _playerStatController.MotionStats.WalkSpeed.Value * AirControlFactor;
            UnityEngine.Vector3 airVelocity = owner.CurrentMoveDirection * airSpeed;

            owner.Rigidbody.linearVelocity = new UnityEngine.Vector3(airVelocity.x, owner.Rigidbody.linearVelocity.y, airVelocity.z);
        }
    }
}
