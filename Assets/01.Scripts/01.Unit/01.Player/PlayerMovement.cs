using UnityEngine;

/// <summary>
/// Player의 모든 
/// </summary>
public class PlayerMovement : UnitMovement
{
    private readonly Transform _cameraTransform;

    public PlayerMovement(Rigidbody rb, Transform transform, Transform cameraTransform, LayerMask groundLayer) 
        : base(rb, transform, groundLayer)
    {
        this._cameraTransform = cameraTransform;
    }

    /// <summary>
    /// Cinemachine 메인 카메라 시점 기준 수평 방향 벡터 산출하는 반환 메서드
    /// </summary>
    public Vector3 CalculateCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.001f || _cameraTransform == null)
            return Vector3.zero;

        Vector3 forward = _cameraTransform.forward;
        Vector3 right = _cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return (forward * input.y + right * input.x).normalized;
    }

    public void Move(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.001f)
        {
            StopMovement();
            return;
        }

        // 1. 경사면에 맞춘 3D 이동 벡터 생성
        Vector3 slopeDirection = AdjustDirectionToSlope(direction);
        Vector3 targetVelocity = slopeDirection * speed;

        // 2. 경사면 이동 시 Y 축 물리 처리
        if (IsGrounded && CurrentSlopeAngle <= MaxSlopeAngle)
        {
            // 평지나 내리막길에서 캐릭터가 뜨지 않고 바닥에 밀착되도록 하향 스냅 적용
            if (Vector3.Dot(targetVelocity, Vector3.up) <= 0.01f)
            {
                targetVelocity.y = -1.5f;
            }
            _rb.linearVelocity = targetVelocity;
        }
        else
        {
            // 공중 상태이거나 오를 수 없는 급경사일 때 → 기존 수직 속도(중력) 보존
            _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);
        }
    }
    /// <summary>
    /// 점프 시 수직 힘을 적용하는 메서드
    /// </summary>
    public void ApplyJump(float jumpForce)
    {
        _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    /// <summary>
    /// 경사면을 타고도 구를 수 있도록 임펄스 적용
    /// </summary>
    public void ApplyRoll(Vector3 targetDirection, float rollForce)
    {
        Vector3 horizeontalDirection = targetDirection;
        horizeontalDirection.y = 0f;

        if (targetDirection.sqrMagnitude > 0.001f)
        {
            _transform.rotation = Quaternion.LookRotation(horizeontalDirection.normalized);
        }

        Vector3 finalRollDirection = AdjustDirectionToSlope(horizeontalDirection.normalized);

        _transform.rotation = Quaternion.LookRotation(finalRollDirection);

        _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        _rb.AddForce(finalRollDirection * rollForce, ForceMode.Impulse);
    }
}
