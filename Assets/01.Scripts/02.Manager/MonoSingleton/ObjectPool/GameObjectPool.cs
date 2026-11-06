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
    private readonly HashSet<UnityEngine.GameObject> _allAllocatedObjects; // 생성된 모든 인스턴스 마스터 추적
    public PoolType PoolIdentifier { get; }

    public GameObjectPool(PoolType poolType, UnityEngine.GameObject prefab, UnityEngine.Transform parent, int initialCount)
    {
        PoolIdentifier = poolType;
        _prefab = prefab;
        _parent = parent;
        _inactiveStack = new Stack<UnityEngine.GameObject>(initialCount > 0 ? initialCount : 5);
        _pooledSet = new HashSet<UnityEngine.GameObject>();
        _allAllocatedObjects = new HashSet<UnityEngine.GameObject>();

        for (int i = 0; i < initialCount; i++)
        {
            CreateNewInstance();
        }
    }

    private UnityEngine.GameObject CreateNewInstance()
    {
        if(_prefab == null) return null;

        UnityEngine.GameObject obj = UnityEngine.Object.Instantiate(_prefab, _parent);
        obj.SetActive(false);
        _inactiveStack.Push(obj);
        _pooledSet.Add(obj);
        _allAllocatedObjects.Add(obj);

        return obj;
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
            obj = CreateNewInstance();
            _inactiveStack.Pop();
            _pooledSet.Remove(obj);
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
        if (_pooledSet.Contains(obj))
        {
            DebugLogger.LogWarningWithTag("ObjectPoolManager", $"[중복 반환 방어] 이미 풀에 반환된 오브젝트: {obj.name}");
            return;
        }

        if (obj.TryGetComponent<IPoolable>(out IPoolable poolable))
        {
            poolable.OnDespawn();
        }

        obj.SetActive(false);
        if (_parent != null)
        {
            obj.transform.SetParent(_parent);
        }

        _inactiveStack.Push(obj);
        _pooledSet.Add(obj);
    }

    public void Clear()
    {
        UnityEngine.GameObject[] allocatedSnapshot = new UnityEngine.GameObject[_allAllocatedObjects.Count];
        _allAllocatedObjects.CopyTo(allocatedSnapshot);

        _allAllocatedObjects.Clear();
        _inactiveStack.Clear();
        _pooledSet.Clear();

        for (int i = 0; i < allocatedSnapshot.Length; i++)
        {
            UnityEngine.GameObject obj = allocatedSnapshot[i];
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        if (_parent != null)
        {
            UnityEngine.Object.Destroy(_parent.gameObject);
        }
    }
}