using Cysharp.Threading.Tasks;
using UnityEngine;
using Util = DebugLogger<SceneBootstrapper>;

public abstract class SceneBootstrapper : MonoBehaviour, ISceneBootstrap
{
    [SerializeField] protected SceneId _sceneId;
    [SerializeField] protected Transform _playerSpawnPoint;

    private bool _isInitialized = false;

    private async void Start()
    {
        if (SceneLoadManager.Instance != null && SceneLoadManager.Instance.IsLoading)
        {
            return;
        }

        // 유니티 에디터에서 해당 씬을 열고 바로 재생 버튼을 눌렀을 때만 자체 기동
        if (!_isInitialized)
        {
            Util.Log($"[에디터 단독 플레이 감지] {_sceneId} 자체 부트스트랩 시작");
            await OnSceneReadyAsync();
        }
    }

    /// <summary>
    /// Scene 셋업
    /// </summary>
    /// <returns></returns>
    public virtual async UniTask OnSceneReadyAsync()
    {
        if (_isInitialized)
        {
            Util.LogWarning($"[{_sceneId}] 이미 초기화가 완료된 부트스트래퍼입니다. 중복 호출을 무시합니다.");
            return;
        }
        _isInitialized = true;

        var ct = this.destroyCancellationToken;

        // 에디터 단독 플레이 시에도 데이터와 풀링 적재를 100% 보장
        if (DataManager.Instance != null)
        {
            await DataManager.Instance.EnsureDataLoadedAsync(ct);
        }

        if (ObjectPoolManager.Instance != null)
        {
            await ObjectPoolManager.Instance.OnSceneLoadCreate(_sceneId);
        }

        ServiceLocator.ClearSceneLocalServices();
        RegisterSceneServices();
        await PreloadSceneAssetsAsync();
        await SetupSceneActorsAsync();
        await SetupSceneUIAsync();
        OnSceneReadyCompleted();
    }

    protected abstract void RegisterSceneServices();
    protected abstract UniTask PreloadSceneAssetsAsync();
    protected abstract UniTask SetupSceneActorsAsync();
    protected abstract UniTask SetupSceneUIAsync();
    protected abstract void OnSceneReadyCompleted();

    protected virtual void OnDestroy()
    {
        ServiceLocator.ClearSceneLocalServices();
        _isInitialized = false;
    }
}
