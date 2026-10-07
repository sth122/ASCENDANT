using UnityEngine;

public abstract class UnitMovement
{
    protected readonly Rigidbody _rb;
    protected readonly Transform _transform;
    protected readonly LayerMask _groundLayer;

    #region 지면 감지 및 경사면 변수
    protected const float SphereRadius = 0.35f;
    protected const float CastOriginOffsetY = 0.4f;
    protected const float CastDistance = 0.15f;
    protected const float MaxSlopeAngle = 45f; // 오를 수 있는 최대 경사 각도
    #endregion

    public RaycastHit GroundHit { get; protected set; }
    public bool IsGrounded { get; protected set; }
    public float CurrentSlopeAngle { get; protected set; }

    protected UnitMovement(Rigidbody rb, Transform transform, LayerMask groundLayer)
    {
        this._rb = rb;
        this._transform = transform;
        this._groundLayer = groundLayer;
    }

    /// <summary>
    /// SphereCast를 통해 지면 접촉 여부와 경사면 법선을 판정
    /// </summary>
    public virtual void CheckGounded()
    {
        Vector3 origin = _transform.position + Vector3.up * CastOriginOffsetY;

        IsGrounded = Physics.SphereCast(
            origin, SphereRadius, Vector3.down, out RaycastHit hit, CastDistance, _groundLayer,
            QueryTriggerInteraction.Ignore);

        GroundHit = hit;

        if (IsGrounded)
        {
            // 바닥과 수직(Vector3.up) 사이의 경사 각도 산출
            CurrentSlopeAngle = Vector3.Angle(Vector3.up, GroundHit.normal);
        }
        else
        {
            CurrentSlopeAngle = 0f;
        }
    }

    /// <summary>
    /// 이동 벡터를 경사면 바닥 표면에 투영(project)하여 경사면 이동 방향을 산출하는 반환 메서드
    /// </summary>
    /// <param name="direction"></param>
    /// <returns></returns>
    public virtual Vector3 AdjustDirectionToSlope(Vector3 direction)
    {
        if(IsGrounded && CurrentSlopeAngle <= MaxSlopeAngle)
        {
            return Vector3.ProjectOnPlane(direction, GroundHit.normal).normalized;
        }
        return direction;
    }

    public virtual void RotateTowards(Vector3 targetDirection, float rotationSpeed, float deltaTime)
    {
        if (targetDirection.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        _transform.rotation = Quaternion.Slerp(_transform.rotation, targetRotation, rotationSpeed * deltaTime);
    }

    public virtual void StopMovement()
    {
        _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
    }

    public virtual void DrawGizmos()
    {
        if (_transform == null) return;

        Vector3 startCenter = _transform.position + Vector3.up * CastOriginOffsetY;
        Vector3 endCenter = startCenter + Vector3.down * CastDistance;

        Gizmos.color = IsGrounded ? new Color(0f, 1f, 0f, 0.6f) : new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireSphere(startCenter, SphereRadius);
        Gizmos.DrawWireSphere(endCenter, SphereRadius);

        if (IsGrounded)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(GroundHit.point, 0.05f);
            Gizmos.DrawRay(GroundHit.point, GroundHit.normal * 0.5f);
        }
    }
}
