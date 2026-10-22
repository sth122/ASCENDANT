using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using Util = DebugLogger<SceneLoadManager>;

/// <summary>
/// 씬 비동기 전환 및 등록된 ILoadable 시스템들의 순차 초기화/정리를 총괄하는 매니저
/// </summary>
public class SceneLoadManager : MonoSingleton<SceneLoadManager>
{
    private readonly List<ILoadable> _loadables = new List<ILoadable>();
    private SceneId _currentScene = SceneId.None;
    private bool _isLoading = false;

    public SceneId CurrentScene => _currentScene;

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();

        // 코어 매니저들을 ILoadable 파이프라인에 등록
        RegisterLoadable(AssetsManager.Instance as ILoadable);
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

    public void UnregisterLoadable(ILoadable loadable)
    {
        _loadables.Remove(loadable);
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

            // 1. 이전 씬 정리 (역순 실행)
            NotifySceneDestroyToLoadables(_currentScene);

            // 2. 로컬 서비스 및 이전 씬 에셋 정리
            ServiceLocator.ClearSceneLocalServices();
            AssetsManager.Instance.ReleaseSceneAssets();
            await UnityEngine.Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: this.destroyCancellationToken);
            System.GC.Collect();

            // 3. Addressables 씬 비동기 로드
            await Addressables.LoadSceneAsync(nextScene.ToString()).ToUniTask(cancellationToken: this.destroyCancellationToken);

            // 현재 씬 ID 갱신 - Bootstrapper 실행 전 선행 갱신으로 인한 Start 실행 차단
            _currentScene = nextScene;

            // 4. 새 씬 내부의 ISceneBootstrap 감지 및 실행
            await ExecuteSceneBootstrapAsync(nextScene);

            // 5. ILoadable 시스템 순차 초기화 (LoadOrder 오름차순)
            await InitializeLoadablesAsync(nextScene);

            Util.Log($"[{nextScene}] 씬 전환 완료");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void NotifySceneDestroyToLoadables(SceneId scene)
    {
        _loadables.RemoveAll(x => x is UnityEngine.Object obj && obj == null);
        for (int i = _loadables.Count - 1; i >= 0; i--)
        {
            _loadables[i].OnSceneDestroy(scene);
        }
    }

    private async UniTask InitializeLoadablesAsync(SceneId scene)
    {
        var sorted = _loadables.OrderBy(x => x.LoadOrder).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            await sorted[i].OnSceneLoadCreate(scene);
        }
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Init(scene);
        }
    }

    private async UniTask ExecuteSceneBootstrapAsync(SceneId scene)
    {
        ISceneBootstrap bootstrap = null;
        var monos = UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsSortMode.None);
        foreach (var mono in monos)
        {
            if (mono is ISceneBootstrap target)
            {
                bootstrap = target;
                break;
            }
        }

        if (bootstrap != null)
        {
            Util.Log($"[{scene}] ISceneBootstrap 감지: 비동기 셋업 실행");
            await bootstrap.OnSceneReadyAsync();
        }
    }
}