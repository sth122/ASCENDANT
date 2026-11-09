using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 플레이어가 보스 방에 진입했을 때 컷신 연출을 트리거하고 보스를 초기화/전투 개시시키는 컴포넌트
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossRoomTrigger : MonoBehaviour
{
    [Header("Target Boss Reference")]
    [SerializeField] private BossBTController _bossController;

    private Collider _triggerCollider;
    private bool _hasTriggered = false;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        if (_triggerCollider != null)
        {
            _triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            _hasTriggered = true;
            if (_triggerCollider != null) _triggerCollider.enabled = false; // 중복 물리 판정 즉시 차단

            HandleBossEncounterFlowAsync(other.transform).Forget();
        }
    }

    private async UniTaskVoid HandleBossEncounterFlowAsync(Transform playerTransform)
    {
        // 1. UIManager를 통해 BossHUDView 로드 및 BossHUDPresenter 바인딩 (이벤트 구독 완료 대기)
        if (UIManager.Instance != null)
        {
            await UIManager.Instance.OpenUIAsync<BossHUDView, BossHUDPresenter, BossHUDModel>(
                UIId.BossHUD, ct: this.destroyCancellationToken
            );
        }

        // 2. 보스 조우 시퀀스 격발 (내부 SetBossEncounterEvent() 호출 -> BossHUDPresenter가 수신하여 페이드인)
        if (_bossController != null && !_bossController.IsDead)
        {
            _bossController.TriggerEncounterSequence(playerTransform);
        }

        // 3. 트리거 오브젝트 비활성화 (씬 리셋 전까지 대기)
        gameObject.SetActive(false);
    }

    public void ResetTrigger()
    {
        _hasTriggered = false;
        if (_triggerCollider != null) _triggerCollider.enabled = true;
        gameObject.SetActive(true);
    }
}