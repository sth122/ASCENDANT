using Cysharp.Threading.Tasks;
using UnityEngine;
using Util = DebugLogger<SceneBootstrapper>;

public abstract class SceneBootstrapper : MonoBehaviour, ISceneBootstrap
{
    [SerializeField] protected SceneId _sceneId;
    [SerializeField] protected Transform _playerSpawnPoint;

    private async void Start()
    {
        // 에디터 단독 플레이 시에도 정상 기동되도록 방어
        if (SceneLoadManager.Instance == null || SceneLoadManager.Instance.CurrentScene != _sceneId)
        {
            Util.Log($"[에디터 단독 플레이 감지] {_sceneId} 직접 부트스트랩");
            await OnSceneReadyAsync();
        }
    }

    public virtual async UniTask OnSceneReadyAsync()
    {
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
    }
}
