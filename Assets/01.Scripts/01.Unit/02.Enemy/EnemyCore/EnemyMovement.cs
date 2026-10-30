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
            if (NavMesh.SamplePosition(_transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
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

    public override void StopMovement()
    {
        base.StopMovement();
        if (_navAgent != null && _navAgent.isOnNavMesh)
        {
            _navAgent.isStopped = true;
            _navAgent.velocity = UnityEngine.Vector3.zero;
        }
    }

    public override void FaceTarget(UnityEngine.Vector3 targetPosition, float rotationSpeed, float deltaTime)
    {
        UnityEngine.Vector3 direction = targetPosition - _transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            RotateTowards(direction.normalized, rotationSpeed, deltaTime);
        }
    }

    /// <summary>
    /// 베이스 지면 기즈모 + NavMeshAgent의 경로(Path Corner) 렌더링
    /// </summary>
    public override void DrawGizmos()
    {
        base.DrawGizmos();

        if (_navAgent != null && _navAgent.hasPath)
        {
            UnityEngine.Gizmos.color = UnityEngine.Color.magenta;
            UnityEngine.Vector3[] corners = _navAgent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                UnityEngine.Gizmos.DrawLine(corners[i], corners[i + 1]);
                UnityEngine.Gizmos.DrawSphere(corners[i], 0.15f);
            }
        }
    }
}
