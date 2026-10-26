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

        // 달리기 키가 이미 눌려져 있고 스태미나가 충분하다면 즉시 Sprint 상태로 승급
        if (owner.InputReader.IsSprinting && _playerStatusController != null && _playerStatusController.HasEnoughStamina(5.0f))
        {
            _playerStateMachine.ChangeState(UnitState.Sprint);
            return;
        }

        UpdateLocomotionAnimation();
    }
    public override void Update()
    {
        base.Update();
        UpdateLocomotionAnimation();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        UnityEngine.Vector3 moveDir = owner.CurrentMoveDirection;
        if (moveDir.sqrMagnitude <= 0.001f)
        {
            owner.StopMovementAPI();
            _playerStateMachine.ChangeState(UnitState.Idle);
            return;
        }

        // 일반 걷기 물리 이동 - 움직이지 않으면 해당 부분 문제인거 체크
        float walkSpeed = _playerStatusController != null && _playerStatusController.PlayerMotionStats != null
            ? _playerStatusController.MotionStatus.WalkSpeed.Value : 0f;

        owner.MoveAPI(moveDir, walkSpeed);

        // 2. 회전 속도 안전 참조 (NullReferenceException 원천 차단)
        float rotSpeed = (_playerStatusController != null && _playerStatusController.PlayerStatusSource != null)
            ? _playerStatusController.PlayerStatusSource.RotationSpeed
            : 16f;


        bool isLockOn = owner.CurrentLockOnTarget != null;

        if (isLockOn)
        {
            // 락온 시 타겟 주시 회전
            UnityEngine.Vector3 targetDir = owner.CurrentLockOnTarget.position - owner.transform.position;
            targetDir.y = 0f;
            if (targetDir.sqrMagnitude > 0.001f)
            {
                owner.RotateTowardsAPI(targetDir.normalized, rotSpeed, UnityEngine.Time.fixedDeltaTime);
            }
        }
        else
        {
            // 비 락온 시 진행 방향으로 회전
            owner.RotateTowardsAPI(moveDir, rotSpeed, UnityEngine.Time.fixedDeltaTime);
        }
    }

    public override void Exit()
    {
        base.Exit();
        _playerAnimController.SetFloat(AnimParamType.MoveX, 0f);
        _playerAnimController.SetFloat(AnimParamType.MoveZ, 0f);
    }

    private void UpdateLocomotionAnimation()
    {
        bool isLockOn = owner.CurrentLockOnTarget != null;
        _playerAnimController.SetBool(AnimParamType.IsLockOn, isLockOn);

        UnityEngine.Vector2 input = owner.InputReader.MoveInput;

        if (isLockOn)
        {
            // 실제 이동 벡터를 캐릭터 로컬 공간으로 투영하여 정확한 8방향 블렌드 트리 매핑
            UnityEngine.Vector3 localMove = owner.transform.InverseTransformDirection(owner.CurrentMoveDirection);
            // Lock-On 시 입력 방향을 그대로 전달하여 8방향 Strafe 블렌딩
            _playerAnimController.SetFloat(AnimParamType.MoveX, localMove.x * 0.5f);
            _playerAnimController.SetFloat(AnimParamType.MoveZ, localMove.z * 0.5f);
        }
        else
        {
            // 일반 걷기 시 전방(MoveZ) 수치만 사용
            _playerAnimController.SetFloat(AnimParamType.MoveX, 0f);
            _playerAnimController.SetFloat(AnimParamType.MoveZ, input.magnitude * 0.5f);
        }
    }
}
