using System.Collections.Generic;
/// <summary>
/// PoolType Enum 기반으로 게임오브젝트를 관리하는 범용 풀 클래스
/// </summary>
public class GameObjectPool
{
    private readonly UnityEngine.GameObject _prefab;
    private readonly UnityEngine.Transform _parent;
    private readonly Stack<UnityEngine.GameObject> _inactiveStack;
    private readonly HashSet<UnityEngine.GameObject> _pooledSet;

    public PoolType PoolIdentifier { get; }

    public GameObjectPool(PoolType poolType, UnityEngine.GameObject prefab, UnityEngine.Transform parent, int initialCount)
    {
        PoolIdentifier = poolType;
        _prefab = prefab;
        _parent = parent;
        _inactiveStack = new Stack<UnityEngine.GameObject>(initialCount > 0 ? initialCount : 5);
        _pooledSet = new HashSet<UnityEngine.GameObject>();

        for (int i = 0; i < initialCount; i++)
        {
            UnityEngine.GameObject obj = UnityEngine.Object.Instantiate(_prefab, _parent);
            obj.SetActive(false);
            _inactiveStack.Push(obj);
            _pooledSet.Add(obj);
        }
    }

    public UnityEngine.GameObject Get(UnityEngine.Vector3 position, UnityEngine.Quaternion rotation, UnityEngine.Transform parent)
    {
        UnityEngine.GameObject obj;
        if (_inactiveStack.Count > 0)
        {
            obj = _inactiveStack.Pop();
            _pooledSet.Remove(obj);
        }
        else
        {
            obj = UnityEngine.Object.Instantiate(_prefab, _parent);
        }

        UnityEngine.Transform tr = obj.transform;
        if (parent != null)
        {
            tr.SetParent(parent, false);
        }
        tr.position = position;
        tr.rotation = rotation;

        obj.SetActive(true);

        if (obj.TryGetComponent<IPoolable>(out IPoolable poolable))
        {
            poolable.OnSpawn();
        }

        return obj;
    }

    public void Return(UnityEngine.GameObject obj)
    {
        if (obj == null) return;

        // 중복 반환 차단
        if (_pooledSet.Contains(obj) || !obj.activeSelf)
        {
            DebugLogger.LogWarningWithTag("ObjectPoolManager", $"[중복 반환 방어] 이미 비활성화된 오브젝트: {obj.name}");
            return;
        }

        if (obj.TryGetComponent<IPoolable>(out IPoolable poolable))
        {
            poolable.OnDespawn();
        }

        obj.SetActive(false);
        obj.transform.SetParent(_parent);

        _inactiveStack.Push(obj);
        _pooledSet.Add(obj);
    }

    public void Clear()
    {
        while (_inactiveStack.Count > 0)
        {
            UnityEngine.GameObject obj = _inactiveStack.Pop();
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
            }
        }
        _inactiveStack.Clear();

        if (_parent != null)
        {
            UnityEngine.Object.Destroy(_parent.gameObject);
        }
    }
}