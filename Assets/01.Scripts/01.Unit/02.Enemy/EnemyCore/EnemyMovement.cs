using UnityEngine.AI;

/// <summary>
/// Enemy 전용 NavMeshAgent 연동 이동 클래스
/// </summary>
public class EnemyMovement : UnitMovement
{
    private readonly NavMeshAgent _navAgent;
    public NavMeshAgent Agent => _navAgent;
    private const float RotationSpeed = 4.5f;

    public EnemyMovement(UnityEngine.Rigidbody rb, UnityEngine.Transform transform, NavMeshAgent agent, 
        UnityEngine.LayerMask groundLayer, GroundCheckConfig groundConfig) : base(rb, transform, groundLayer, groundConfig)
    {
        this._navAgent = agent;
        if (_navAgent != null)
        {
            _navAgent.updateRotation = false;
            _navAgent.updatePosition = true;
            _navAgent.stoppingDistance = 1.6f;
        }
    }

    public override bool CheckGrounded()
    {
        base.CheckGrounded();

        // 물리 판정이 실패했더라도 NavMeshAgent가 NavMesh 위에 정상 안착되어 있다면 지면 접지 인정
        if (!IsGrounded && _navAgent != null && _navAgent.isOnNavMesh)
        {
            IsGrounded = true;
            IsOnSlope = false;
            IsOnSteepSlope = false;
            GroundNormal = UnityEngine.Vector3.up;
        }

        return IsGrounded;
    }

    /// <summary>
    /// Chase와 Patrol의 목적에 맞게 stoppingDistance 설정
    /// </summary>
    public void SetStoppingDistance(float distance)
    {
        if (_navAgent != null)
        {
            _navAgent.stoppingDistance = distance;
        }
    }

    /// <summary>
    /// 네비게이션 목적지 경로 설정
    /// </summary>
    public void SetDestination(UnityEngine.Vector3 targetPosition, float speed)
    {
        if (_navAgent == null) return;

        if (!_navAgent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(_transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                _navAgent.Warp(hit.position);
            }
            else return;
        }

        _navAgent.isStopped = false;
        _navAgent.speed = speed;
        _navAgent.SetDestination(targetPosition);
    }

    /// <summary>
    /// 이동 중인 에이전트의 경로 진행 방향을 매 프레임 부드럽게 응시하도록 회전 갱신 (Update에서 매 프레임 호출 필수)
    /// </summary>
    public void TickLocomotionRotation(float deltaTime, float rotationSpeed = RotationSpeed)
    {
        if (_navAgent == null || !_navAgent.isOnNavMesh) return;

        UnityEngine.Vector3 moveDir = UnityEngine.Vector3.zero;

        // 1. desiredVelocity가 유효하면 이동 추진 속도 방향 사용
        if (_navAgent.desiredVelocity.sqrMagnitude > 0.01f)
        {
            moveDir = _navAgent.desiredVelocity;
        }
        // 2. desiredVelocity가 작다면 경로상 다음 코너(steeringTarget) 방향 사용
        else if (_navAgent.hasPath)
        {
            moveDir = _navAgent.steeringTarget - _transform.position;
        }

        moveDir.y = 0f;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            RotateTowards(moveDir.normalized, rotationSpeed, deltaTime);
        }
    }

    /// <summary>
    /// 루트 모션 기반 이동을 위한 NavMeshAgent 일시 분리
    /// </summary>
    public void BeginRootMotionMovement()
    {
        if (_navAgent != null && _navAgent.isOnNavMesh)
        {
            _navAgent.isStopped = true;
            _navAgent.updatePosition = false;
            _navAgent.updateRotation = false;
        }
    }

    /// <summary>
    /// 루트 모션 종료 후 NavMeshAgent 위치 동기화 및 복구
    /// </summary>
    public void EndRootMotionMovement()
    {
        if (_navAgent == null) return;

        UnityEngine.Vector3 currentPos = _transform.position;
        if (NavMesh.SamplePosition(currentPos, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
        {
            _navAgent.Warp(hit.position);
        }
        else
        {
            _navAgent.Warp(currentPos);
        }

        _navAgent.updatePosition = true;
        _navAgent.updateRotation = false;
        _navAgent.isStopped = true;
    }

    public override void StopMovement()
    {
        base.StopMovement();
        if (_navAgent != null && _navAgent.isOnNavMesh)
        {
            _navAgent.isStopped = true;
            _navAgent.velocity = UnityEngine.Vector3.zero;
        }
    }
    public void ApplyRootMotionDelta(UnityEngine.Vector3 deltaPosition, UnityEngine.Quaternion deltaRotation, bool isAirborne)
    {
        if (_transform == null) return;

        if (!isAirborne)
        {
            deltaPosition.y = 0f; // 지면 모션인 경우 Y축 튐 방지
        }

        _transform.position += deltaPosition;
        _transform.rotation *= deltaRotation;
    }
}
