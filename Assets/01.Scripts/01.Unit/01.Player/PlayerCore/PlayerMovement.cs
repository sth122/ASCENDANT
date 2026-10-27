using UnityEngine;

/// <summary>
/// Player의 모든 이동 관련 물리 연산을 전담하는 클래스
/// </summary>
public class PlayerMovement : UnitMovement
{
    private Transform _cameraTransform;

    public PlayerMovement(Rigidbody rb, Transform transform, Transform cameraTransform, LayerMask groundLayer)
        : base(rb, transform, groundLayer)
    {
        CacheMainCamera();
    }

    /// <summary>
    /// 메인 카메라 캐싱
    /// </summary>
    public void CacheMainCamera()
    {
        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }
    }

    /// <summary>
    /// Cinemachine 메인 카메라 시점 기준 수평 방향 벡터 산출하는 반환 메서드
    /// </summary>
    public Vector3 CalculateCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.001f || _cameraTransform == null)
            return Vector3.zero;

        // 카메라 참조가 소실된 경우 재탐색
        if (_cameraTransform == null)
        {
            CacheMainCamera();
            if (_cameraTransform == null) return Vector3.zero;
        }

        Vector3 forward = _cameraTransform.forward;
        Vector3 right = _cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return (forward * input.y + right * input.x).normalized;
    }

    /// <summary>
    /// Player 이동 + 경사면 처리
    /// </summary>
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
            // 상향 경사 등반 시에는 경사면 y속도를 온전히 보존하고, 평지/내리막에서만 지면 밀착 스냅 적용
            if (slopeDirection.y > 0.01f)
            {
                _rb.linearVelocity = targetVelocity;
            }
            else
            {
                float downSnap = Mathf.Clamp(-0.5f - (CurrentSlopeAngle * 0.05f), -2.5f, -0.5f);
                targetVelocity.y = downSnap;
                _rb.linearVelocity = targetVelocity;
            }
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
    public void ApplyRoll(Vector3 rollDirection, float maxSpeed, float normalizedTime)
    {
        float speedMultiplier = Mathf.Clamp01(1.0f - normalizedTime);
        float currentSpeed = maxSpeed * speedMultiplier;

        Vector3 slopeRollDir = AdjustDirectionToSlope(rollDirection);
        if (IsGrounded && CurrentSlopeAngle <= MaxSlopeAngle)
        {
            // 지면 위에서 구를 때는 경사면을 따라 흐르도록 완전한 투영 속도 반영
            _rb.linearVelocity = slopeRollDir * currentSpeed;
        }
        else
        {
            // 공중에 떴을 때만 기존 중력 보존
            Vector3 targetVel = slopeRollDir * currentSpeed;
            targetVel.y = _rb.linearVelocity.y;
            _rb.linearVelocity = targetVel;
        }
    }
}
