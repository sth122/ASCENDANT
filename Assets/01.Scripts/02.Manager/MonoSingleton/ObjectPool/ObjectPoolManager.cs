using System.Collections.Generic;
using UnityEngine;
using Util = DebugLogger<ObjectPoolManager>;

// <summary>
/// PoolType Enum 기반으로 인게임 객체를 동기 및 비동기로 풀링하는 싱글톤 매니저
/// </summary>
public class ObjectPoolManager : MonoSingleton<ObjectPoolManager>, ILoadable
{
    public int LoadOrder => 5;

    private readonly Dictionary<PoolType, GameObjectPool> _pools = new Dictionary<PoolType, GameObjectPool>();
    private readonly HashSet<PoolType> _sceneDependentPoolTypes = new HashSet<PoolType>();
    private readonly HashSet<PoolType> _globalGameplayPoolTypes = new HashSet<PoolType>();
    private Transform _poolRoot;

    private bool _isPoolReady = false;
    private bool _isGlobalGameplayLoaded = false;
    public bool IsPoolReady => _isPoolReady;

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();
        EnsureRoot();
    }

    private void EnsureRoot()
    {
        if (_poolRoot == null)
        {
            _poolRoot = new GameObject("Pool_Root").transform;
            _poolRoot.SetParent(transform);
        }
    }

    public async Cysharp.Threading.Tasks.UniTask EnsurePoolReadyAsync(System.Threading.CancellationToken ct = default)
    {
        if (_isPoolReady) return;

        // 아직 OnSceneLoadCreate가 호출되지 않은 경우 대기
        await Cysharp.Threading.Tasks.UniTask.WaitUntil(() => _isPoolReady, cancellationToken: ct);
    }

    #region ILoadable 구현부 - 추후 각 Scene 별로 Pooling 할 객체 분리 후 구현 완료
    public async Cysharp.Threading.Tasks.UniTask OnSceneLoadCreate(SceneId scene)
    {
        _isPoolReady = false;

        try
        {
            if (DataManager.Instance == null || !DataManager.Instance.TryGetSingle<ScenePoolConfigSO>(out var configSO))
            {
                return;
            }

            // 1. 인게임 씬(TitleScene 제외) 진입 시 플레이어 공용 전역 풀 최초 1회 적재
            if (scene != SceneId.TitleScene && scene != SceneId.None && !_isGlobalGameplayLoaded)
            {
                var globalItems = configSO.GlobalGameplayItems;
                for (int i = 0; i < globalItems.Count; i++)
                {
                    var item = globalItems[i];
                    await PreloadPoolAsync(item.Type, item.GetAddressableKey(), item.PreloadCount,
                        this.destroyCancellationToken, isSceneDependent: false);
                    _globalGameplayPoolTypes.Add(item.Type);
                }
                _isGlobalGameplayLoaded = true;
                Util.Log($"인게임 전역 공용 풀 {globalItems.Count}종 최초 적재 완료");
            }

            // 2. 현재 진입하는 씬 전용 풀 적재
            if (configSO.TryGetPoolItemsForScene(scene, out List<ScenePoolConfigSO.PoolItem> sceneItems))
            {
                for (int i = 0; i < sceneItems.Count; i++)
                {
                    var item = sceneItems[i];
                    await PreloadPoolAsync(item.Type, item.GetAddressableKey(), item.PreloadCount,
                        this.destroyCancellationToken, isSceneDependent: true);
                }
                Util.Log($"[{scene}] 씬 전용 오브젝트 풀 {sceneItems.Count}종 적재 완료");
            }
        }
        catch (System.Exception ex)
        {
            Util.LogError($"풀 초기화 실패: {ex.Message}");
        }
        finally
        {
            _isPoolReady = true;
        }
    }
    public void Init(SceneId scene) => Util.Log($"[{scene}] 풀 매니저 준비 완료");
    public void OnSceneDestroy(SceneId scene)
    {
        _isPoolReady = false;

        // 1. 씬 전용 풀은 완전 파괴
        ClearSceneDependentPools();

        // 2. 인게임 전역 풀은 파괴하지 않고 미반환 활성 인스턴스만 회수
        foreach (PoolType globalType in _globalGameplayPoolTypes)
        {
            if (_pools.TryGetValue(globalType, out GameObjectPool pool))
            {
                pool.RecallAllActive();
            }
        }
    }
    #endregion

    #region 동기 등록 및 풀 생성
    public void RegisterPool(PoolType poolType, GameObject prefab, int initialCount = 1, bool isSceneDependent = false)
    {
        if (_pools.ContainsKey(poolType)) return;

        EnsureRoot();

        Transform folder = new GameObject($"(Pool_{poolType})").transform;
        folder.SetParent(_poolRoot);

        _pools[poolType] = new GameObjectPool(poolType, prefab, folder, initialCount);

        if (isSceneDependent)
        {
            _sceneDependentPoolTypes.Add(poolType);
        }

        Util.Log($"[풀 등록 완료] {poolType} (초기 수량: {initialCount}, 씬 종속: {isSceneDependent})");
    }
    #endregion

    #region 비동기 프리로드 
    public async Cysharp.Threading.Tasks.UniTask PreloadPoolAsync(PoolType poolType, string addressableKey, int initialCount = 1, System.Threading.CancellationToken ct = default, bool isSceneDependent = false)
    {
        if (_pools.ContainsKey(poolType)) return;

        GameObject prefab = await AssetsManager.Instance.LoadAssetAsync<GameObject>(addressableKey, ct, isGlobal: true);

        if (prefab != null)
        {
            RegisterPool(poolType, prefab, initialCount, isSceneDependent);
        }
        else
        {
            Util.Log($"[비동기 풀 프리로드 실패] Key: {addressableKey}");
        }
    }
    #endregion

    #region Spawn & Despawn
    /// <summary>
    /// 동기 Spawn
    /// </summary>
    public GameObject Spawn(PoolType poolType, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
    {
        if (_pools.TryGetValue(poolType, out GameObjectPool pool))
        {
            return pool.Get(position, rotation, parent);
        }

        Util.LogError($"등록되지 않은 풀 요청: {poolType}");
        return null;
    }

    /// <summary>
    /// 컴포넌트 타입 제네릭 동기 Spawn
    /// </summary>
    public T Spawn<T>(PoolType poolType, Vector3 position = default, Quaternion rotation = default, Transform parent = null) where T : Component
    {
        GameObject go = Spawn(poolType, position, rotation, parent);
        if (go != null && go.TryGetComponent<T>(out T comp))
        {
            return comp;
        }

        Util.LogError($"풀 객체에서 컴포넌트({typeof(T).Name}) 탐색 실패: {poolType}");
        return null;
    }

    /// <summary>
    /// 비동기 Spawn (해당 풀이 아직 없을 경우 비동기 로드 후 즉시 인스턴스 반환)
    /// </summary>
    public async Cysharp.Threading.Tasks.UniTask<T> SpawnAsync<T>(PoolType poolType, string fallbackAddressableKey, Vector3 position = default, Quaternion rotation = default, Transform parent = null, System.Threading.CancellationToken ct = default, bool isSceneDependent = false) where T : Component
    {
        if (!_pools.ContainsKey(poolType))
        {
            await PreloadPoolAsync(poolType, fallbackAddressableKey, 5, ct, isSceneDependent);
        }

        return Spawn<T>(poolType, position, rotation, parent);
    }

    /// <summary>
    /// GameObject 반환
    /// </summary>
    public void Despawn(PoolType poolType, GameObject obj)
    {
        if (obj == null) return;

        if (_pools.TryGetValue(poolType, out GameObjectPool pool))
        {
            pool.Return(obj);
        }
        else
        {
            Util.LogError($"풀을 찾을 수 없어 객체를 파괴: {poolType}");
            Destroy(obj);
        }
    }

    /// <summary>
    /// Component 반환
    /// </summary>
    public void Despawn<T>(PoolType poolType, T comp) where T : Component
    {
        if (comp == null) return;
        Despawn(poolType, comp.gameObject);
    }
    #endregion

    /// <summary>
    /// 씬 전환 시 씬 종속 풀을 정리할 때 호출
    /// </summary>
    public void ClearPool(PoolType poolType)
    {
        if (_pools.TryGetValue(poolType, out GameObjectPool pool))
        {
            pool.Clear();
            _pools.Remove(poolType);
            _sceneDependentPoolTypes.Remove(poolType);
            _globalGameplayPoolTypes.Remove(poolType);
            Util.Log($"[단일 풀 메모리 정리] {poolType}");
        }
    }

    public void ClearSceneDependentPools()
    {
        List<PoolType> toClear = new List<PoolType>(_sceneDependentPoolTypes);
        for(int i = 0; i < toClear.Count; i++)
        {
            ClearPool(toClear[i]);
        }
        _sceneDependentPoolTypes.Clear();
        Util.Log("[씬 종속 오브젝트 풀 메모리 일괄 반환 완료]");
    }

    /// <summary>
    /// 타이틀 화면으로 복귀할 때 인게임 전역 풀까지 비울 때 호출
    /// </summary>
    public void ClearGlobalGameplayPools()
    {
        List<PoolType> toClear = new List<PoolType>(_globalGameplayPoolTypes);
        for (int i = 0; i < toClear.Count; i++)
        {
            ClearPool(toClear[i]);
        }
        _globalGameplayPoolTypes.Clear();
        _isGlobalGameplayLoaded = false;
    }

    protected override void OnDestroy()
    {
        foreach (GameObjectPool pool in _pools.Values)
        {
            pool.Clear();
        }
        _pools.Clear();
        _sceneDependentPoolTypes.Clear();
        _globalGameplayPoolTypes.Clear();
        base.OnDestroy();
    }
}
