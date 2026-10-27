using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerUpperBodyAligner : MonoBehaviour
{
    [Header("Bone Settings")]
    [SerializeField] private HumanBodyBones _spineBoneType = HumanBodyBones.Chest;
    [SerializeField] private Vector3 _rotationOffset = Vector3.zero;
    [SerializeField] private float _alignSlerpSpeed = 12.0f;

    private PlayerController _playerController;
    private Animator _animator;
    private Transform _chestBone;
    private Transform _mainCameraTransform;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _animator = GetComponent<Animator>();

        if (_animator != null && _animator.isHuman)
        {
            _chestBone = _animator.GetBoneTransform(_spineBoneType);
            if (_chestBone == null)
            {
                _chestBone = _animator.GetBoneTransform(HumanBodyBones.Spine);
            }
        }
        if (Camera.main != null)
        {
            _mainCameraTransform = Camera.main.transform;
        }
    }

    /// <summary>
    /// 애니메이터의 모든 레이어(Base, UpperBody) 평가가 끝난 직후 본 각도를 최종 보정
    /// </summary>
    private void LateUpdate()
    {
        if (_chestBone == null || _playerController == null) return;

        // 가드(방패 들기) 상태일 때만 상체 각도를 정면으로 정렬
        if (_playerController.InputReader != null && _playerController.InputReader.IsGuarding)
        {
            Vector3 targetForward;

            // 1. 락온 타겟이 있으면 타겟을 향해 상체 정렬, 없으면 캐릭터/카메라 전방 응시
            if (_playerController.CurrentLockOnTarget != null)
            {
                targetForward = _playerController.CurrentLockOnTarget.position - _chestBone.position;
            }
            else
            {
                targetForward = _mainCameraTransform != null ? _mainCameraTransform.forward : transform.forward;
            }

            targetForward.y = 0f; // 수평 유지 (위아래 꺾임 방지)

            if (targetForward.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetForward.normalized);

                // 골반 틸트의 영향을 받지 않도록 월드 회전값을 전방 각도로 보정
                _chestBone.rotation = Quaternion.Slerp(_chestBone.rotation, targetRotation, _alignSlerpSpeed * Time.deltaTime);
            }
        }
    }
}