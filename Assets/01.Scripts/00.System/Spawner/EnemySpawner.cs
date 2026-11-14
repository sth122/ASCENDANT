using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using Util = DebugLogger<EnemySpawner>;

/// <summary>
/// 소울라이크 레벨 디자인 배치 앵커 컴포넌트
/// PoolType과 UnitId 설정을 통해 모든 종류의 Enemy를 범용적으로 스폰·초기화하며,
/// 화톳불(세이브 터미널) 상호작용 시 사망한 개체를 자동으로 리셋 및 부활시킵니다.
/// </summary>
public class EnemySpawner : MonoBehaviour, ISceneLoadable
{
    [Header("Monster Identity Configuration")]
    [SerializeField] private PoolType _enemyPoolType = PoolType.NightmareCreature;
    [SerializeField] private UnitId _unitId = UnitId.NightmareCreature;

    [Header("Scene Load Order")]
    [SerializeField] private int _sceneLoadOrder = 10;

    private EnemyBTController _spawnedEnemy;
    private bool _isEnemyDead = false;

    public int SceneLoadOrder => _sceneLoadOrder;

    private void OnEnable()
    {
        EventBusSystem.Subscribe<BonfireRestedEvent>(HandleBonfireRested);
    }

    private void OnDisable()
    {
        EventBusSystem.Unsubscribe<BonfireRestedEvent>(HandleBonfireRested);
    }
    #region ISceneLoadable
    public UniTask OnSceneObjectLoadAsync(SceneId scene, CancellationToken ct)
    {
        SpawnEnemy();
        return UniTask.CompletedTask;
    }

    public void OnSceneObjectReady(SceneId scene)
    {
        // 모든 씬 액터 배치가 완료된 시점에 추가 연동이 필요할 경우 사용
    }
    #endregion

    /// <summary>
    /// PoolType에 맞는 EnemyController를 획득하고, DataManager에서 UnitId 스탯을 찾아 확정 주입
    /// </summary>
    public void SpawnEnemy()
    {
        if (_spawnedEnemy != null && !_isEnemyDead) return;
        if (ObjectPoolManager.Instance == null) return;

        _spawnedEnemy = ObjectPoolManager.Instance.Spawn<EnemyBTController>(
            _enemyPoolType, transform.position, transform.rotation
        );

        if (_spawnedEnemy == null)
        {
            Util.LogError($"[{gameObject.name}] 풀에서 적 획득 실패: {_enemyPoolType}");
            return;
        }

        _isEnemyDead = false;

        if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<EnemyInfoSO>(out var enemyStatusSO))
        {
            if (enemyStatusSO.TryGetData(_unitId, out EnemyInfo baseStatus))
            {
                _spawnedEnemy.BindStatusData((EnemyInfo)baseStatus.Clone());
            }
        }

        if (ServiceLocator.TryGet<IEnemyManager>(out var enemyManager))
        {
            enemyManager.RegisterEnemy(_spawnedEnemy);
        }

        _spawnedEnemy.StatusController.OnDieEvent -= HandleEnemyDied;
        _spawnedEnemy.StatusController.OnDieEvent += HandleEnemyDied;
    }

    private void HandleEnemyDied()
    {
        _isEnemyDead = true;
        if (_spawnedEnemy != null && _spawnedEnemy.StatusController != null)
        {
            _spawnedEnemy.StatusController.OnDieEvent -= HandleEnemyDied;
        }
    }

    private void HandleBonfireRested(BonfireRestedEvent evt)
    {
        ResetSpawnerOnBonfire();
    }

    public void ResetSpawnerOnBonfire()
    {
        if (_spawnedEnemy != null && !_isEnemyDead)
        {
            _spawnedEnemy.transform.SetPositionAndRotation(transform.position, transform.rotation);
            if (_spawnedEnemy.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) && agent.isOnNavMesh)
            {
                agent.Warp(transform.position);
            }
            _spawnedEnemy.StatusController.HealHp(_spawnedEnemy.StatusController.MaxHp);
            _spawnedEnemy.StatusController.ResetPoiseToDefault();
            _spawnedEnemy.ClearTarget();
        }
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
}