using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
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

        // 코어 매니저들을 ILoadable 파이프라인에 등록
        RegisterLoadable(AssetsManager.Instance);
        RegisterLoadable(ObjectPoolManager.Instance);
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

    public async UniTask LoadSceneFlowAsync(SceneId nextScene)
    {
        if (_isLoading)
        {
            Util.LogWarning("이미 씬 전환이 진행 중입니다.");
            return;
        }
        _isLoading = true;

        try
        {
            Util.Log($"[{_currentScene}] -> [{nextScene}] 씬 전환 파이프라인 개시");

            FadeCurtainView fadeCurtain = null;
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

            // 1. 이전 씬 정리 (역순 실행)
            NotifySceneDestroyToLoadables(_currentScene);

            // 2. 로컬 서비스 및 이전 씬 에셋 정리
            ServiceLocator.ClearSceneLocalServices();
            AssetsManager.Instance.ReleaseSceneAssets();
            await UnityEngine.Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: this.destroyCancellationToken);
            System.GC.Collect();

            // 3. 이전 어드레서블 씬 언로드 
            if (_hasActiveSceneHandle)
            {
                await Addressables.UnloadSceneAsync(_currentSceneHandle).ToUniTask(cancellationToken: this.destroyCancellationToken);
                _hasActiveSceneHandle = false;
            }

            // 4. Addressables 씬 비동기 로드 및 핸들 캐싱
            AsyncOperationHandle<SceneInstance> sceneHandle = Addressables.LoadSceneAsync(nextScene.ToString());
            SceneInstance sceneInstance = await sceneHandle.ToUniTask(cancellationToken: this.destroyCancellationToken);
            _currentSceneHandle = sceneHandle;
            _hasActiveSceneHandle = true;

            // 현재 씬 ID 갱신
            _currentScene = nextScene;

            // 5. ILoadable 시스템 순차 초기화 (LoadOrder 오름차순)
            await InitializeLoadablesAsync(nextScene);

            // 6. 등록된 씬 부트스트래퍼 실행 (IoC 기반 즉각 실행)
            await ExecuteSceneBootstrapAsync(nextScene);

            if (fadeCurtain != null)
            {
                await fadeCurtain.FadeInAsync(0.5f, this.destroyCancellationToken);
            }

            Util.Log($"[{nextScene}] 씬 전환 완료");
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