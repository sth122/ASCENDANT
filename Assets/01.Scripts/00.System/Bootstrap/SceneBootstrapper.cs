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
        // 에디터 단독 플레이 시에도 정상 기동되도록 방어
        if (!_isInitialized)
        {
            if (SceneLoadManager.Instance == null || SceneLoadManager.Instance.CurrentScene != _sceneId)
            {
                Util.Log($"[에디터 단독 플레이 감지] {_sceneId} 직접 부트스트랩");
                await OnSceneReadyAsync();
            }
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
