using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Util = DebugLogger<AssetsManager>;

public class AssetsManager : MonoSingleton<AssetsManager>, ILoadable
{
    public int LoadOrder => 0;

    // 전역 유지 에셋 핸들
    private readonly Dictionary<string, AsyncOperationHandle> _globalAssetHandles = new Dictionary<string, AsyncOperationHandle>();

    // 씬 전환 시 언로드되는 씬 종속 에셋 핸들
    private readonly Dictionary<string, AsyncOperationHandle> _sceneAssetHandles = new Dictionary<string, AsyncOperationHandle>();

    // 1회성 인스턴스화 오브젝트 캐시
    private readonly Dictionary<UnityEngine.GameObject, AsyncOperationHandle> _instanceHandles = new Dictionary<UnityEngine.GameObject, AsyncOperationHandle>();

    // 씬 전환 시 함께 해제되어야 할 씬 종속 인스턴스 추적용 해시셋
    private readonly HashSet<UnityEngine.GameObject> _sceneInstances = new HashSet<UnityEngine.GameObject>();

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();
    }

    #region ILoadable 구현부
    public UniTask OnSceneLoadCreate(SceneId scene) => UniTask.CompletedTask;
    public void Init(SceneId scene) => Util.Log($"[{scene}] AssetsManager 준비 완료");
    public void OnSceneDestroy(SceneId scene) => ReleaseSceneAssets();
    #endregion

    #region UniTask(비동기) 에셋 로드
    /// <summary>
    /// 단일 에셋 비동기 로드
    /// </summary>
    public async UniTask<T> LoadAssetAsync<T>(string addressableKey, System.Threading.CancellationToken ct = default, bool isGlobal = false) where T : UnityEngine.Object

    {
        Dictionary<string, AsyncOperationHandle> targetDict = isGlobal ? _globalAssetHandles : _sceneAssetHandles;

        if (targetDict.TryGetValue(addressableKey, out AsyncOperationHandle existingHandle))
        {
            return existingHandle.Result as T;
        }

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(addressableKey);
        T result = await handle.ToUniTask(cancellationToken: ct);

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            targetDict[addressableKey] = handle;
            Util.Log($"[비동기 로드 성공] Key: {addressableKey} (Global: {isGlobal})");
            return result;
        }

        Util.LogError($"[비동기 로드 실패] Key: {addressableKey}");
        return null;
    }

    /// <summary>
    /// Label 단위 비동기 에셋 일괄 로드
    /// </summary>
    public async UniTask<IList<T>> LoadAssetsByLabelAsync<T>(string label, System.Threading.CancellationToken ct = default,
        bool isGlobal = false, System.Action<T> onSingleLoaded = null) where T : UnityEngine.Object
    {
        Dictionary<string, AsyncOperationHandle> targetDict = isGlobal ? _globalAssetHandles : _sceneAssetHandles;

        if (targetDict.TryGetValue(label, out AsyncOperationHandle existingHandle))
        {
            return existingHandle.Result as IList<T>;
        }

        AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(label, onSingleLoaded);
        IList<T> result = await handle.ToUniTask( cancellationToken: ct);

        if(handle.Status == AsyncOperationStatus.Succeeded) {
            targetDict[label] = handle;
            Util.Log($"[라벨 일괄 로드 성공] Label: {label} (개수: {result.Count})");
            return result;
        }

        Util.LogError($"[라벨 일괄 로드 실패] Label: {label}");
        return null;
    }

    /// <summary>
    /// 풀링하지 않는 1회성 프리팹 비동기 인스턴스화
    /// </summary>
    public async UniTask<UnityEngine.GameObject> InstantiateAsync(string addressableKey, UnityEngine.Transform parent = null,
        bool inWorldSpace = false, 
        bool isSceneAsset = true, // 씬 종속 에셋 여부 (전역 에셋은 false)
        System.Threading.CancellationToken ct = default)
    {
        try
        {
            AsyncOperationHandle<UnityEngine.GameObject> handle = Addressables.InstantiateAsync(addressableKey, parent, inWorldSpace);
            UnityEngine.GameObject instance = await handle.WithCancellation(ct);

            if (instance == null)
            {
                Util.LogError($"인스턴스 생성 실패 (NULL 반환): {addressableKey}");
                return null;
            }

            // 모든 인스턴스는 매핑에 등록
            _instanceHandles[instance] = handle;

            // 씬 로컬 에셋일 때만 등록하여 씬 전환 시 일괄 해제 추적 해시셋에 등록
            if (isSceneAsset)
            {
                _sceneInstances.Add(instance);
            }

            return instance;
        }
        catch (System.Exception ex)
        {
            Util.LogError($"InstantiateAsync 실패 [{addressableKey}]: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region 동기 에셋 로드
    /// <summary>
    /// 단일 에셋 동기 로드
    /// </summary>
    public T LoadAssetSync<T>(string addressableKey, bool isGlobal = false) where T : UnityEngine.Object
    {
        Dictionary<string, AsyncOperationHandle> targetDict = isGlobal ? _globalAssetHandles : _sceneAssetHandles;

        if (targetDict.TryGetValue(addressableKey, out AsyncOperationHandle existingHandle))
        {
            return existingHandle.Result as T;
        }

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(addressableKey);
        T result = handle.WaitForCompletion();

        if(handle.Status == AsyncOperationStatus.Succeeded)
        {
            targetDict[addressableKey] = handle;
            Util.Log($"[동기 로드 성공] Key: {addressableKey} (Global: {isGlobal})");
            return result;
        }

        Util.LogError($"[동기 로드 실패] Key: {addressableKey}");
        return null;
    }

    /// <summary>
    /// 1회성 프리팹 동기 인스턴스화
    /// </summary>
    public UnityEngine.GameObject InstantiateSync(string addressableKey, UnityEngine.Transform parent = null)
    {
        AsyncOperationHandle<UnityEngine.GameObject> handle = Addressables.InstantiateAsync(addressableKey);
        UnityEngine.GameObject instance = handle.WaitForCompletion();

        if(handle.Status == AsyncOperationStatus.Succeeded)
        {
            _instanceHandles[instance] = handle;
            Util.Log($"[인스턴스 동기 생성] Key: {addressableKey}");
            return instance;
        }

        Util.LogError($"[인스턴스 동기 생성 실패] Key: {addressableKey}");
        return null;
    }
    #endregion

    #region 메모리 해제 및 생명 주기
    /// <summary>
    /// 생성된 인스턴스 해제
    /// </summary>
    public void ReleaseInstance(UnityEngine.GameObject instance)
    {
        if (instance == null) return;

        if(_instanceHandles.TryGetValue(instance, out AsyncOperationHandle handle))
        {
            Addressables.ReleaseInstance(instance);
            _instanceHandles.Remove(instance);
            _sceneInstances.Remove(instance);
            Util.Log($"[인스턴스 해제] {instance.name}");
        }
        else
        {
            Destroy(instance);
        }
    }

    /// <summary>
    /// 씬 전환 시 호출되어 씬 종속 에셋과 1회성 인스턴스 일괄 해제
    /// </summary>
    public void ReleaseSceneAssets()
    {
        int releasedInstanceCount = 0;

        // 1. 씬 종속 인스턴스 해제
        foreach (UnityEngine.GameObject instance in _sceneInstances)
        {
            if (instance != null)
            {
                Addressables.ReleaseInstance(instance);
                _instanceHandles.Remove(instance);
                releasedInstanceCount++;
            }
        }
        _sceneInstances.Clear();

        // 2. 씬 종속 에셋 핸들 해제
        int releasedCount = _sceneAssetHandles.Count;
        foreach(AsyncOperationHandle handle in _sceneAssetHandles.Values)
        {
            Addressables.Release(handle);
        }
        _sceneAssetHandles.Clear();

        Util.Log($"[씬 에셋 메모리 정리 완료] 총 {releasedCount}개 핸들 반환");
    }

    /// <summary>
    /// 게임 종료 시 전역 에셋을 포함한 전체 메모리 강제 정리
    /// </summary>
    public void ReleaseAll()
    {
        // 씬 종속 에셋 정리
        ReleaseSceneAssets();

        // 전역 인스턴스 정리
        foreach (KeyValuePair<UnityEngine.GameObject, AsyncOperationHandle> pair in _instanceHandles)
        {
            if (pair.Key != null)
            {
                Addressables.ReleaseInstance(pair.Key);
            }
        }
        _instanceHandles.Clear();

        // 전역 에셋 핸들 정리
        foreach (AsyncOperationHandle handle in _globalAssetHandles.Values)
        {
            Addressables.Release(handle);
        }
        _globalAssetHandles.Clear();

        Util.Log("[전역 에셋 포함 전체 메모리 정리 완료]");
    }
    #endregion
}