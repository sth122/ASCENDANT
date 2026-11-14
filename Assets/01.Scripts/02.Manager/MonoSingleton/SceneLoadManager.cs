using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Util = DebugLogger<SceneLoadManager>;

/// <summary>
/// 씬 비동기 전환 및 등록된 ILoadable 시스템들의 순차 초기화/정리를 총괄하는 매니저
/// </summary>
public class SceneLoadManager : MonoSingleton<SceneLoadManager>
{
    #region 변수
    private readonly List<ILoadable> _loadables = new List<ILoadable>();
    private AsyncOperationHandle<SceneInstance> _currentSceneHandle;        // 어드레서블 씬 핸들 보관 (씬 전환 시 언로드 누수 방지)
    private ISceneBootstrap _activeSceneBootstrap;
    private SceneId _currentScene = SceneId.None;

    private bool _hasActiveSceneHandle = false;
    private bool _isLoading = false;
    #endregion

    #region Property
    public SceneId CurrentScene => _currentScene;
    public bool IsLoading => _isLoading;
    #endregion

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (System.Enum.TryParse(activeSceneName, out SceneId parsedScene))
        {
            _currentScene = parsedScene;
        }

        // 모든 전역 ILoadable 매니저를 LoadOrder 순서 파이프라인에 명시적 등록
        RegisterLoadable(AssetsManager.Instance);
        RegisterLoadable(DataManager.Instance);
        RegisterLoadable(SoundManager.Instance);
        RegisterLoadable(ObjectPoolManager.Instance);
        RegisterLoadable(VFXManager.Instance);
        RegisterLoadable(UIManager.Instance);
    }

    public void RegisterLoadable(ILoadable loadable)
    {
        if (loadable != null && !_loadables.Contains(loadable))
        {
            _loadables.Add(loadable);
        }
    }

    public void UnregisterLoadable(ILoadable loadable) => _loadables.Remove(loadable);

    /// <summary>
    /// 씬 부트스트래퍼가 Awake 시점에 스스로를 등록
    /// </summary>
    public void RegisterBootstrapper(ISceneBootstrap bootstrap) => _activeSceneBootstrap = bootstrap;

    public void UnregisterBootstrapper(ISceneBootstrap bootstrap)
    {
        if (_activeSceneBootstrap == bootstrap)
        {
            _activeSceneBootstrap = null;
        }
    }

    /// <summary>
    /// 콜드 스타트(TitleScene) 또는 에디터 단독 플레이 시 전역 ILoadable + 씬 부트스트랩을 단일 파이프라인으로 실행
    /// </summary>
    public async UniTask RunInitialOrStandaloneBootstrapAsync(SceneId scene, ISceneBootstrap bootstrap)
    {
        if (_isLoading) return;
        _isLoading = true;
        _currentScene = scene;
        _activeSceneBootstrap = bootstrap;

        try
        {
            await InitializeLoadablesAsync(scene);
            await ExecuteSceneBootstrapAsync(scene);
        }
        finally
        {
            _isLoading = false;
        }
    }

    public async UniTask LoadSceneFlowAsync(SceneId nextScene)
    {
        if (_isLoading) return;
        _isLoading = true;
        FadeCurtainView fadeCurtain = null;

        try
        {
            // 1. 암막 호출 및 화면 Fade Out
            if (UIManager.Instance != null)
            {
                fadeCurtain = await UIManager.Instance.OpenUIAsync<FadeCurtainView, FadeCurtainPresenter, FadeCurtainModel>(
                    UIId.FadeCurtainUI, ct: this.destroyCancellationToken
                );

                if (fadeCurtain != null)
                {
                    await fadeCurtain.FadeOutAsync(0.4f, this.destroyCancellationToken);
                }
            }

            // 2. 화면이 완전히 암전된 상태에서 로딩 화면
            if (UIManager.Instance != null)
            {
                await UIManager.Instance.OpenUIAsync<LoadingScreenView, LoadingScreenPresenter, LoadingScreenModel>(
                    UIId.LoadingScreenUI, ct: this.destroyCancellationToken
                );
            }

            EventBusSystem.Publish(new SceneLoadingProgressEvent(0.1f, "Loading."));

            // 3. 로컬 UI 및 이전 씬 리소스 정리
            NotifySceneDestroyToLoadables(_currentScene);
            ServiceLocator.ClearSceneLocalServices();
            AssetsManager.Instance.ReleaseSceneAssets();

            // 4. 이전 씬 언로드
            if (_hasActiveSceneHandle)
            {
                await Addressables.UnloadSceneAsync(_currentSceneHandle).ToUniTask(cancellationToken: this.destroyCancellationToken);
                _hasActiveSceneHandle = false;
            }

            EventBusSystem.Publish(new SceneLoadingProgressEvent(0.35f, "Loading.."));

            // 5. 신규 씬 비동기 로드
            AsyncOperationHandle<SceneInstance> sceneHandle = Addressables.LoadSceneAsync(nextScene.ToString());
            await sceneHandle.ToUniTask(cancellationToken: this.destroyCancellationToken);
            _currentSceneHandle = sceneHandle;
            _hasActiveSceneHandle = true;
            _currentScene = nextScene;

            EventBusSystem.Publish(new SceneLoadingProgressEvent(0.7f, "Loading..."));

            // 6. 시스템 순차 초기화 (ObjectPoolManager 씬 종속 풀 로드 등)
            await InitializeLoadablesAsync(nextScene);

            EventBusSystem.Publish(new SceneLoadingProgressEvent(0.9f, "Loading."));

            // 7. 씬 부트스트랩 실행 (플레이어 스폰, 카메라 타깃 바인딩)
            await ExecuteSceneBootstrapAsync(nextScene);

            EventBusSystem.Publish(new SceneLoadingProgressEvent(1.0f, "Loading.."));

            // 액터 위치 및 카메라 블렌드 완벽 안착을 위한 1프레임 대기
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken: this.destroyCancellationToken);
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.2f), cancellationToken: this.destroyCancellationToken);

            // 8. 씬 준비가 완전히 끝났으므로 로딩 화면 제거
            if (UIManager.Instance != null)
            {
                UIManager.Instance.CloseUI(UIId.LoadingScreenUI);
            }

            // 9. 암막을 걷어내며 화면 페이드 인 (Fade In)
            if (fadeCurtain != null)
            {
                await fadeCurtain.FadeInAsync(0.5f, this.destroyCancellationToken);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void NotifySceneDestroyToLoadables(SceneId scene)
    {
        _loadables.RemoveAll(HandleNullLoadablePredicate);
        for (int i = _loadables.Count - 1; i >= 0; i--)
        {
            _loadables[i].OnSceneDestroy(scene);
        }
    }

    private bool HandleNullLoadablePredicate(ILoadable loadable) => loadable is UnityEngine.Object obj && obj == null;

    private async UniTask InitializeLoadablesAsync(SceneId scene)
    {
        var sorted = _loadables.OrderBy(GetLoadOrderKey).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            await sorted[i].OnSceneLoadCreate(scene);
        }
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Init(scene);
        }
    }
    private int GetLoadOrderKey(ILoadable loadable) => loadable.LoadOrder;

    private async UniTask ExecuteSceneBootstrapAsync(SceneId scene)
    {
        if (_activeSceneBootstrap != null)
        {
            Util.Log($"[{scene}] 등록된 ISceneBootstrap 감지: 비동기 셋업 실행");
            await _activeSceneBootstrap.OnSceneReadyAsync();
        }
        else
        {
            Util.LogWarning($"[{scene}] 등록된 ISceneBootstrap이 없습니다.");
        }
    }
}