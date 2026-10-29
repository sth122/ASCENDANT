using UnityEngine;

/// <summary>
/// Player의 모든 이동 관련 물리 연산을 전담하는 클래스
/// </summary>
public class PlayerMovement : UnitMovement
{
    private Transform _cameraTransform;
    private const float SteepSlopeSlideSpeed = 8.0f;

    public PlayerMovement(Rigidbody rb, Transform transform, Transform cameraTransform, LayerMask groundLayer, GroundCheckConfig groundConfig)
        : base(rb, transform, groundLayer, groundConfig)
    {
        _cameraTransform = cameraTransform;
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
        if (input.sqrMagnitude < 0.001f) return Vector3.zero;

        if (_cameraTransform == null) CacheMainCamera();

        Vector3 camForward = _cameraTransform != null ? _cameraTransform.forward : Vector3.forward;
        Vector3 camRight = _cameraTransform != null ? _cameraTransform.right : Vector3.right;

        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        return (camForward * input.y + camRight * input.x).normalized;
    }

    /// <summary>
    /// Player 이동 + 경사면 처리
    /// </summary>
    public void Move(Vector3 direction, float speed)
    {
        if (_rb == null) return;

        // 1. 최대 경사각 초과 급경사: 아래로 강제 미끄러짐
        if (IsOnSteepSlope)
        {
            Vector3 slideDir = GetSteepSlopeSlideDirection();
            _rb.linearVelocity = slideDir * SteepSlopeSlideSpeed;
            return;
        }

        // 2. 이동 입력이 없는 정지 상태
        if (direction.sqrMagnitude < 0.001f)
        {
            if (IsGrounded)
            {
                // 경사면 정지 시 중력에 의해 슬금슬금 미끄러지는 현상 방지
                _rb.linearVelocity = Vector3.zero;
            }
            else
            {
                // 공중에서는 수직 낙하 속도만 유지
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            }
            return;
        }

        // 3. 정상 이동
        if (IsGrounded)
        {
            if (IsOnSlope)
            {
                // 경사면 표면 방향으로 투영하여 오르막 감속 및 내리막 붕 뜸 방지
                Vector3 slopeDirection = ProjectDirectionOnSlope(direction);
                _rb.linearVelocity = slopeDirection * speed;
            }
            else
            {
                // 평지 이동
                Vector3 targetVelocity = direction * speed;
                targetVelocity.y = _rb.linearVelocity.y;
                _rb.linearVelocity = targetVelocity;
            }
        }
        else
        {
            // 공중 제어 (Air Control)
            Vector3 targetVelocity = direction * speed;
            targetVelocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = targetVelocity;
        }
    }
    /// <summary>
    /// 점프 시 수직 힘을 적용하는 메서드
    /// </summary>
    public void ApplyJump(float jumpForce)
    {
        if (_rb == null) return;

        IsGrounded = false;
        IsOnSlope = false;

        _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    /// <summary>
    /// 경사면을 타고도 구를 수 있도록 임펄스 적용
    /// </summary>
    public void ApplyRoll(Vector3 rollDirection, float maxSpeed, float normalizedTime)
    {
        if (_rb == null) return;

        float speedMultiplier = Mathf.Sin(Mathf.Clamp01(normalizedTime) * Mathf.PI);
        Vector3 finalRollDir = rollDirection;

        if (IsGrounded && IsOnSlope)
        {
            finalRollDir = ProjectDirectionOnSlope(rollDirection);
        }

        _rb.linearVelocity = finalRollDir * (maxSpeed * speedMultiplier);
    }
}
