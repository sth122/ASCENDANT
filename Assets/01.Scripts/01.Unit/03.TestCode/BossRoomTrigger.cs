using UnityEngine;

/// <summary>
/// 플레이어가 보스 방에 진입했을 때 컷신 연출을 트리거하고 보스를 초기화/전투 개시시키는 컴포넌트
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossRoomTrigger : MonoBehaviour
{
    [Header("Target Boss Reference")]
    [SerializeField] private BossBTController _bossController;

    [Header("Door / Fog Wall Barriers (선택 사항)")]
    [SerializeField] private GameObject[] _fogWalls;

    private bool _hasTriggered = false;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            _hasTriggered = true;

            // 안개벽/출입문 차단
            SetFogWallsActive(true);

            // 보스 조우 시퀀스 격발
            if (_bossController != null)
            {
                _bossController.TriggerEncounterSequence(other.transform);
            }

            // 트리거 오브젝트 비활성화 (중복 격발 방지)
            gameObject.SetActive(false);
        }
    }

    private void SetFogWallsActive(bool isActive)
    {
        if (_fogWalls == null) return;
        for (int i = 0; i < _fogWalls.Length; i++)
        {
            if (_fogWalls[i] != null) _fogWalls[i].SetActive(isActive);
        }
    }
}