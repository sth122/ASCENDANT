using UnityEngine;

/// <summary>
/// 플레이어 전방 및 주변의 IInteractable 객체를 탐색하고,
/// F키(Interact) 입력 시 상호작용을 격발하는 컴포넌트
/// </summary>
public class PlayerInteractionHandler : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float _detectRadius = 2.4f;
    [SerializeField] private float _viewAngleDot = 0.35f; // 전방 약 140도 부채꼴
    [SerializeField] private LayerMask _interactableLayer;

    private readonly Collider[] _hitBuffer = new Collider[8];
    private PlayerController _player;
    private IInteractable _currentInteractable;

    private float _detectTimer = 0f;
    private const float DetectInterval = 0.08f;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (_player != null && _player.InputReader != null)
        {
            _player.InputReader.OnInteractEvent -= HandleInteractPressed;
            _player.InputReader.OnInteractEvent += HandleInteractPressed;
        }
    }

    private void OnDisable()
    {
        if (_player != null && _player.InputReader != null)
        {
            _player.InputReader.OnInteractEvent -= HandleInteractPressed;
        }
        ClearCurrentInteractable();
    }

    private void Update()
    {

        // 행동 불가 상태(구르기, 피격, 넉다운, 스턴, 사망) 중에는 상호작용 비활성화
        if (!CanInteractInCurrentState())
        {
            if (_currentInteractable != null) ClearCurrentInteractable();
            return;
        }

        _detectTimer += Time.deltaTime;
        if (_detectTimer >= DetectInterval)
        {
            _detectTimer = 0f;
            DetectNearbyInteractable();
        }
    }

    private bool CanInteractInCurrentState()
    {
        if (_player == null || _player.StateMachineInstance == null) return false;

        IState curState = _player.StateMachineInstance.CurrentState;
        return curState is PlayerIdleState || curState is PlayerMoveState || curState is PlayerSprintState;
    }

    private void DetectNearbyInteractable()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, _detectRadius, _hitBuffer, _interactableLayer);
        IInteractable bestCandidate = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hitBuffer[i];
            if (col == null) continue;

            IInteractable interactable = col.GetComponentInParent<IInteractable>();
            if (interactable == null) continue;

            Vector3 toTarget = col.transform.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            // 시선(정면) 내적 검사 (등 뒤 물체 배제)
            float dot = Vector3.Dot(transform.forward, toTarget.normalized);
            if (dot < _viewAngleDot) continue;

            // 정면을 바라볼수록 높은 점수 부여
            float score = (dot * 2.0f) - (dist / _detectRadius);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = interactable;
            }
        }
        System.Array.Clear(_hitBuffer, 0, hitCount);

        if (bestCandidate != _currentInteractable)
        {
            _currentInteractable = bestCandidate;
            UpdateInteractionHUD();
        }
    }

    private void HandleInteractPressed()
    {
        if (!CanInteractInCurrentState()) return;

        if (_currentInteractable != null && _player != null)
        {
            _currentInteractable.OnInteract(_player);
        }
    }

    private void UpdateInteractionHUD()
    {
        bool hasTarget = _currentInteractable != null;
        string prompt = hasTarget ? _currentInteractable.InteractionPrompt : string.Empty;

        EventBusSystem.Publish(new InteractionPromptEvent(hasTarget, prompt));
    }

    private void ClearCurrentInteractable()
    {
        _currentInteractable = null;
        EventBusSystem.Publish(new InteractionPromptEvent(false, string.Empty));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, _detectRadius);
    }
}