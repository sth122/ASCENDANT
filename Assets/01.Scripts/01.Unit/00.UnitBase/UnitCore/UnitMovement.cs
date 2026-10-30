using UnityEngine;

[System.Serializable]
public struct GroundCheckConfig
{
    [Tooltip("지면 감지 스피어 구체 반경")]
    public float SphereRadius;

    [Tooltip("스피어캐스트 발사 원점 Y축 오프셋")]
    public float CastOriginOffsetY;

    [Tooltip("스피어캐스트 하향 투영 거리")]
    public float CastDistance;

    [Tooltip("올라갈 수 있는 최대 경사 각도")]
    public float MaxSlopeAngle;

    public static GroundCheckConfig Default => new GroundCheckConfig
    {
        SphereRadius = 0.35f,
        CastOriginOffsetY = 0.4f,
        CastDistance = 0.15f,
        MaxSlopeAngle = 45f
    };

    public GroundCheckConfig(float radius, float offsetY, float distance, float maxSlope)
    {
        SphereRadius = radius;
        CastOriginOffsetY = offsetY;
        CastDistance = distance;
        MaxSlopeAngle = maxSlope;
    }
}

[System.Serializable]
public abstract class UnitMovement
{
    protected readonly Rigidbody _rb;
    protected readonly Transform _transform;
    protected readonly LayerMask _groundLayer;
    protected GroundCheckConfig _groundConfig;

    #region 지면 및 경사면 프로퍼티
    public bool IsGrounded { get; protected set; }
    public bool IsOnSlope { get; protected set; }
    public bool IsOnSteepSlope { get; protected set; }
    public float CurrentSlopeAngle { get; protected set; }
    public Vector3 GroundNormal { get; protected set; } = Vector3.up;
    public RaycastHit GroundHit => _groundHit;
    public GroundCheckConfig GroundConfig => _groundConfig;
    #endregion

    protected RaycastHit _groundHit;

    protected UnitMovement(Rigidbody rb, Transform transform, LayerMask groundLayer, GroundCheckConfig groundConfig)
    {
        this._rb = rb;
        this._transform = transform;
        this._groundLayer = groundLayer;
        this._groundConfig = groundConfig;
    }

    /// <summary>
    /// SphereCast를 통해 지면 접촉 여부와 경사면 법선을 판정
    /// </summary>
    public virtual bool CheckGrounded()
    {
        if (_transform == null) return false;

        Vector3 origin = _transform.position + Vector3.up * _groundConfig.CastOriginOffsetY;

        bool checkGrounded = Physics.SphereCast(origin, _groundConfig.SphereRadius, Vector3.down, out _groundHit,
            _groundConfig.CastDistance, _groundLayer, QueryTriggerInteraction.Ignore);

        if (checkGrounded)
        {
            GroundNormal = _groundHit.normal;
            CurrentSlopeAngle = Vector3.Angle(Vector3.up, GroundNormal);

            // 정상 등판 가능한 경사각 판정
            if (CurrentSlopeAngle <= _groundConfig.MaxSlopeAngle)
            {
                IsGrounded = true;
                IsOnSteepSlope = false;
                // 평지가 아닌 경사면인지 판별 (0.5도 초과)
                IsOnSlope = CurrentSlopeAngle > 0.5f;
            }
            else
            {
                // 최대 경사각을 초과한 급경사 (등판 불가 지대)
                IsGrounded = false;
                IsOnSlope = false;
                IsOnSteepSlope = true;
            }

            return IsGrounded;
        }
        GroundNormal = Vector3.up;
        CurrentSlopeAngle = 0f;
        IsGrounded = false;
        IsOnSlope = false;
        IsOnSteepSlope = false;
        return false;
    }

    /// <summary>
    /// 수평 입력 방향 벡터를 경사면 표면 법선 평면에 평행하게 투영
    /// </summary>
    public Vector3 ProjectDirectionOnSlope(Vector3 horizontalDirection)
    {
        return Vector3.ProjectOnPlane(horizontalDirection, GroundNormal).normalized;
    }

    /// <summary>
    /// 등판 불가 급경사에서 미끄러져 내려가는 중력 방향 벡터 산출
    /// </summary>
    public Vector3 GetSteepSlopeSlideDirection()
    {
        return Vector3.ProjectOnPlane(Vector3.down, GroundNormal).normalized;
    }

    public virtual void RotateTowards(Vector3 targetDirection, float rotationSpeed, float deltaTime)
    {
        if (targetDirection.sqrMagnitude < 0.001f || _transform == null) return;

        targetDirection.y = 0f;
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized);
        _transform.rotation = Quaternion.Slerp(_transform.rotation, targetRotation, rotationSpeed * deltaTime);
    }

    /// <summary>
    /// 타깃 위치를 향해 Y축 평면 기준으로 부드럽게 선회
    /// </summary>
    public virtual void FaceTarget(Vector3 targetPosition, float rotationSpeed, float deltaTime)
    {
        if (_transform == null) return;

        Vector3 direction = targetPosition - _transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            RotateTowards(direction.normalized, rotationSpeed, deltaTime);
        }
    }

    /// <summary>
    /// 타깃 방향으로 정렬 (선딜레이 종료 후 타격 개시 시점용)
    /// </summary>
    public virtual void SnapToTarget(Vector3 targetPosition)
    {
        if (_transform == null) return;

        Vector3 direction = targetPosition - _transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            _transform.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }

    public virtual void StopMovement()
    {
        // Enemy NavMesh 충돌 문제
        if (_rb != null && !_rb.isKinematic)
        {
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }
    }

    public virtual void UpdateGroundConfig(GroundCheckConfig newConfig) => _groundConfig = newConfig;

    public virtual void DrawGizmos()
    {
        if (_transform == null) return;

        Vector3 origin = _transform.position + Vector3.up * _groundConfig.CastOriginOffsetY;
        Vector3 end = origin + Vector3.down * _groundConfig.CastDistance;

        // 접지: 녹색, 공중: 적색, 급경사: 주황색
        if (IsOnSteepSlope)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
        }
        else
        {
            Gizmos.color = IsGrounded ? new Color(0f, 1f, 0.2f, 0.7f) : new Color(1f, 0.15f, 0.15f, 0.7f);
        }

        Gizmos.DrawWireSphere(origin, _groundConfig.SphereRadius);
        Gizmos.DrawWireSphere(end, _groundConfig.SphereRadius);

        Gizmos.DrawLine(origin + Vector3.left * _groundConfig.SphereRadius, end + Vector3.left * _groundConfig.SphereRadius);
        Gizmos.DrawLine(origin + Vector3.right * _groundConfig.SphereRadius, end + Vector3.right * _groundConfig.SphereRadius);
        Gizmos.DrawLine(origin + Vector3.forward * _groundConfig.SphereRadius, end + Vector3.forward * _groundConfig.SphereRadius);
        Gizmos.DrawLine(origin + Vector3.back * _groundConfig.SphereRadius, end + Vector3.back * _groundConfig.SphereRadius);

        // 경사면 표면 법선(Normal) 레이 렌더링
        if (IsGrounded && IsOnSlope)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(_transform.position, GroundNormal * 1.5f);
        }
    }
}

