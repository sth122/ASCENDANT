using UnityEngine;
using Util = DebugLogger<EnemySpawner>;

/// <summary>
/// 소울라이크 레벨 디자인 배치 앵커 컴포넌트
/// PoolType과 UnitId 설정을 통해 모든 종류의 Enemy를 범용적으로 스폰·초기화하며,
/// 화롯불(세이브 터미널) 상호작용 시 사망한 개체를 자동으로 리셋 및 부활시킵니다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Monster Identity Configuration")]
    [Tooltip("스폰할 적의 풀링 타입")]
    [SerializeField] private PoolType _enemyPoolType = PoolType.NightmareCreature;

    [Tooltip("적 스탯 SO 조회를 위한 고유 UnitId")]
    [SerializeField] private UnitId _unitId = UnitId.NightmareCreature;

    [Header("Spawn Settings")]
    [SerializeField] private bool _spawnOnStart = true;

    private EnemyController _spawnedEnemy;
    private bool _isEnemyDead = false;

    private void Start()
    {
        // 씬 부트스트랩 완료 후 스폰 개시
        if (_spawnOnStart)
        {
            SpawnEnemy();
        }
    }

    /// <summary>
    /// PoolType에 맞는 EnemyController를 획득하고, DataManager에서 UnitId 스탯을 찾아 확정 주입
    /// </summary>
    public void SpawnEnemy()
    {
        if (_spawnedEnemy != null && !_isEnemyDead) return;

        // 1. 오브젝트 풀에서 컴포넌트 타입으로 인스턴스 획득 (어떤 Enemy 파생 클래스든 수용)
        _spawnedEnemy = ObjectPoolManager.Instance.Spawn<EnemyController>(
            _enemyPoolType, transform.position, transform.rotation
        );

        if (_spawnedEnemy == null)
        {
            Util.LogError($"[{gameObject.name}] 풀에서 적 획득 실패: {_enemyPoolType}");
            return;
        }

        _isEnemyDead = false;

        // 2. DataManager에서 해당 UnitId의 최신 기획 스탯 데이터 획득
        EnemyStatus runtimeStatus = null;
        if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<EnemyStatusSO>(out var enemyStatusSO))
        {
            if (enemyStatusSO.TryGetData(_unitId, out EnemyStatus baseStatus))
            {
                runtimeStatus = (EnemyStatus)baseStatus.Clone();
            }
        }

        // 3. 다형성 초기화 메서드 호출 (위치 및 스탯 확정 바인딩)
        _spawnedEnemy.SpawnInit(transform.position, runtimeStatus);

        // 4. 사망 이벤트 리스너 바인딩 (화롯불 리셋 추적용)
        _spawnedEnemy.StatusController.OnDieEvent -= HandleEnemyDied;
        _spawnedEnemy.StatusController.OnDieEvent += HandleEnemyDied;

        Util.Log($"[{gameObject.name}] {_enemyPoolType} (ID: {_unitId}) 스폰 및 초기화 완료");
    }

    private void HandleEnemyDied()
    {
        _isEnemyDead = true;
        if (_spawnedEnemy != null)
        {
            _spawnedEnemy.StatusController.OnDieEvent -= HandleEnemyDied;
        }
        Util.Log($"[{gameObject.name}] 관리 대상 적 사망 확인: {_unitId}");
    }

    /// <summary>
    /// 화롯불(세이브 터미널)과 상호작용하여 월드가 리셋될 때 호출
    /// </summary>
    public void ResetSpawnerOnBonfire()
    {
        // 살아있는 적은 위치와 체력을 초기 상태로 리셋
        if (_spawnedEnemy != null && !_isEnemyDead)
        {
            EnemyStatus runtimeStatus = null;
            if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<EnemyStatusSO>(out var enemyStatusSO))
            {
                if (enemyStatusSO.TryGetData(_unitId, out EnemyStatus baseStatus))
                {
                    runtimeStatus = (EnemyStatus)baseStatus.Clone();
                }
            }
            _spawnedEnemy.SpawnInit(transform.position, runtimeStatus);
        }
        // 이미 사망한 적은 풀에서 새로 스폰하여 제자리에 배치
        else if (_isEnemyDead)
        {
            SpawnEnemy();
        }
    }

    private void OnDestroy()
    {
        if (_spawnedEnemy != null && _spawnedEnemy.StatusController != null)
        {
            _spawnedEnemy.StatusController.OnDieEvent -= HandleEnemyDied;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
    }
}