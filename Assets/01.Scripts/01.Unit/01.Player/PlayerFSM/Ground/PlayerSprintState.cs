public class PlayerSprintState : PlayerGroundState
{
    // 기획 밸런스 수치
    private const float SprintStaminaCostPerSec = 16.0f; // 초당 스태미나 소모량
    private const float MinimumSprintStamina = 5.0f;      // 달리기 시작에 필요한 최소 스태미나

    public PlayerSprintState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        // 1. 최소 스태미나 미달 시 즉시 일반 걷기 상태로 강등
        if (_playerStatusController != null && !_playerStatusController.HasEnoughStamina(MinimumSprintStamina))
        {
            _playerStateMachine.ChangeState(UnitState.Move);
            return;
        }

        UpdateSprintLocomotionAnimation();
    }
    public override void Update()
    {
        base.Update();
        UpdateSprintLocomotionAnimation();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // 1. 이동 입력 방향 확인
        UnityEngine.Vector3 moveDir = owner.CurrentMoveDirection;
        if (moveDir.sqrMagnitude <= 0.001f)
        {
            owner.StopMovementAPI();
            _playerStateMachine.ChangeState(UnitState.Idle);
            return;
        }

        // 2. 실시간 스태미나 지속 차감
        float staminaCost = SprintStaminaCostPerSec * UnityEngine.Time.fixedDeltaTime;
        if (_playerStatusController != null)
        {
            if (!_playerStatusController.HasEnoughStamina(staminaCost))
            {
                // 스태미나가 완전히 고갈되면 일반 걷기로 강제 전환
                _playerStateMachine.ChangeState(UnitState.Move);
                return;
            }

            _playerStatusController.ConsumeStamina(staminaCost);
        }

        // 3. 스프린트 물리 이동 실행 - 움직이지 않으면 해당 부분 문제인거 체크
        float sprintSpeed = _playerStatusController != null && _playerStatusController.PlayerMotionStats != null
            ? _playerStatusController.PlayerMotionStats.SprintSpeed.Value            : 0f;

        owner.MoveAPI(moveDir, sprintSpeed);

        // 4. 회전 제어 (락온 중이라도 전력 질주 시에는 이동 진행 방향을 바라보며 뜀)
        owner.RotateTowardsAPI(moveDir, _playerStatusController.PlayerStatusSource.RotationSpeed, UnityEngine.Time.fixedDeltaTime);
    }

    private void UpdateSprintLocomotionAnimation()
    {
        _playerAnimController.SetBool(AnimParamType.IsLockOn, false);
        _playerAnimController.SetFloat(AnimParamType.MoveX, 0f);
        // 달리기 상태에서는 MoveZ 블렌드 트리를 전력 질주(1.0f) 수치로 고정
        _playerAnimController.SetFloat(AnimParamType.MoveZ, 1.0f);
    }
}