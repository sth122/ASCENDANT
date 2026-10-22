using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Util = DebugLogger<ObjectPoolManager>;

// <summary>
/// PoolType Enum 기반으로 인게임 객체를 동기 및 비동기로 풀링하는 싱글톤 매니저
/// </summary>
public class ObjectPoolManager : MonoSingleton<ObjectPoolManager>
{
    private readonly Dictionary<PoolType, GameObjectPool> _pools = new Dictionary<PoolType, GameObjectPool>();
    private Transform _poolRoot;

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

    #region 동기 등록 및 풀 생성
    public void RegisterPool(PoolType poolType, GameObject prefab, int initialCount = 1)
    {
        if (_pools.ContainsKey(poolType))
        {
            Util.Log($"이미 등록된 풀: {poolType}");
            return;
        }

        EnsureRoot();

        Transform folder = new GameObject($"(Pool_{poolType}").transform;
        folder.SetParent(_poolRoot);

        _pools[poolType] = new GameObjectPool(poolType, prefab, folder, initialCount);
        Util.Log($"[풀 등록 완료] {poolType} (초기 수량: {initialCount})");
    }
    #endregion

    #region 비동기 프리로드 
    public async UniTask PreloadPoolAsync(PoolType poolType, string addressableKey, int initialCount = 1, System.Threading.CancellationToken ct = default)
    {
        if (_pools.ContainsKey(poolType)) return;

        GameObject prefab = await AssetsManager.Instance.LoadAssetAsync<GameObject>(addressableKey, ct, isGlobal: true);

        if (prefab != null)
        {
            RegisterPool(poolType, prefab, initialCount);
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
    public async UniTask<T> SpawnAsync<T>(PoolType poolType, string fallbackAddressableKey, Vector3 position = default, Quaternion rotation = default, Transform parent = null, System.Threading.CancellationToken ct = default) where T : Component
    {
        if (!_pools.ContainsKey(poolType))
        {
            await PreloadPoolAsync(poolType, fallbackAddressableKey, 5, ct);
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
            Util.Log($"[단일 풀 메모리 정리] {poolType}");
        }
    }

    protected override void OnDestroy()
    {
        foreach (GameObjectPool pool in _pools.Values)
        {
            pool.Clear();
        }
        _pools.Clear();
        base.OnDestroy();
    }
}
