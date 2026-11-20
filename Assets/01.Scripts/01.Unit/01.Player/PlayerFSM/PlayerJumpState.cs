/// <summary>
/// Player 점프 동작을 전담하는 Player FSM 상태 클래스
/// 진입 시 수직 임펄스(AddForce)를 가하고 스태미나를 차감하며,
/// 하강 중 지면 감지 시점에 도달하면 착지 후 조건에 맞는 상태로 복귀
/// </summary>
public class PlayerJumpState : PlayerBaseState
{  
    public const float JumpStaminaCost = 12.0f;     // 임시 외부 참조 허용. 소모값 SO나 Module을 따로 만들 예정
    private const float JumpCooldown = 0.15f;       // 도약 직후 즉시 착지 판정되는 것 방지
    private float _timeInAir = 0f;

    public PlayerJumpState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        _timeInAir = 0f;
        owner.InputReader.ClearCommand(InputCommandType.Jump);

        // 방어선
        if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(JumpStaminaCost))
        {
            if (owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
            {
                _playerStateMachine.ChangeState(owner.InputReader.IsSprinting ? UnitState.Sprint : UnitState.Move);
            }
            else
            {
                _playerStateMachine.ChangeState(UnitState.Idle);
            }
            return;
        }


        // 1. 스태미나 차감
        _playerStatusController?.ConsumeStamina(JumpStaminaCost);
        _playerAnimController.SetTrigger(AnimParamType.Jump);

        // 2. JumpForce 반영  - 점프하지 않으면 해당 부분 문제인거 체크
        float jumpForce = _playerStatusController != null && _playerStatusController.PlayerMotionStats != null ?
            _playerStatusController.PlayerMotionStats.JumpForce.Value : 0.0f;

        owner.ApplyJumpAPI(jumpForce);
    }

    public override void Update()
    {
        base.Update();
        _timeInAir += UnityEngine.Time.deltaTime;

        if (_timeInAir >= JumpCooldown)
        {
            // 1. 점프 정점을 지나 하강하기 시작하면 Fall 상태로 전이하여 체공 시퀀스 인계
            if (owner.Rigidbody.linearVelocity.y <= 0f && !owner.Movement.IsGrounded)
            {
                _playerStateMachine.ChangeState(UnitState.Fall);
                return;
            }

            // 2. 즉시 착지한 경우 기본 지상 상태 복귀
            if (owner.Movement.IsGrounded)
            {
                owner.InputReader.ClearCommand(InputCommandType.Jump);
                if (owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
                {
                    _playerStateMachine.ChangeState(UnitState.Move);
                }
                else
                {
                    _playerStateMachine.ChangeState(UnitState.Idle);
                }
                return;
            }
        }
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        if(owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
            if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
            {
                bool isSprint = owner.InputReader.IsSprinting;
                float speed = isSprint ? _playerStatusController.PlayerMotionStats.SprintSpeed.Value
                    : _playerStatusController.PlayerMotionStats.WalkSpeed.Value;

                UnityEngine.Vector3 airVelocity = owner.CurrentMoveDirection * speed;
                owner.Rigidbody.linearVelocity = new UnityEngine.Vector3(airVelocity.x, owner.Rigidbody.linearVelocity.y, airVelocity.z);

                float rotSpeed = (_playerStatusController != null && _playerStatusController.PlayerInfoSource != null)
                    ? _playerStatusController.PlayerInfoSource.RotationSpeed : 16f;
                owner.RotateTowardsAPI(owner.CurrentMoveDirection, rotSpeed, UnityEngine.Time.fixedDeltaTime);
            }
    }
}
