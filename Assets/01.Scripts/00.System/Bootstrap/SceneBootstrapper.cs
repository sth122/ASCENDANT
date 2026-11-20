using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Util = DebugLogger<SceneBootstrapper>;

public abstract class SceneBootstrapper : MonoBehaviour, ISceneBootstrap
{
    [SerializeField] protected SceneId _sceneId;
    [SerializeField] protected Transform _playerSpawnPoint;

    private bool _isInitialized = false;

    protected virtual void Awake()
    {
        if (SceneLoadManager.Instance != null)
        {
            SceneLoadManager.Instance.RegisterBootstrapper(this);
        }
    }

    private async void Start()
    {
        if (SceneLoadManager.Instance != null && SceneLoadManager.Instance.IsLoading) return;

        // 유니티 에디터에서 해당 씬을 열고 바로 재생 버튼을 눌렀을 때만 자체 기동
        if (!_isInitialized && SceneLoadManager.Instance != null)
        {
            Util.Log($"[단독/최초 기동 감지] {_sceneId} 통합 부트스트랩 파이프라인 시작");
            try
            {
                await SceneLoadManager.Instance.RunInitialOrStandaloneBootstrapAsync(_sceneId, this);
            }
            catch (System.Exception ex)
            {
                Util.LogError($"[{_sceneId}] 부트스트랩 기동 실패: {ex}");
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

        var ct = this.destroyCancellationToken;

        // 1. 씬 로컬 서비스 등록 (IPlayerProvider, IEnemyManager)
        RegisterSceneServices();

        // 2. 씬 내 배치된 ISceneLoadable 오브젝트 순차 로드 및 초기화
        await InitializeSceneLocalLoadablesAsync(ct);

        // 3. 씬 전용 프리로드 에셋
        await PreloadSceneAssetsAsync();

        // 4. 플레이어 및 주요 액터 스폰/바인딩
        await SetupSceneActorsAsync();

        // 5. 씬 UI 오픈
        await SetupSceneUIAsync();

        // 6. 상태 전이 및 완료 브로드캐스트
        OnSceneReadyCompleted();
    }

    private async UniTask InitializeSceneLocalLoadablesAsync(System.Threading.CancellationToken ct)
    {
        MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        List<ISceneLoadable> sceneLoadables = new List<ISceneLoadable>();

        for (int i = 0; i < allBehaviours.Length; i++)
        {
            if (allBehaviours[i] is ISceneLoadable loadable)
            {
                sceneLoadables.Add(loadable);
            }
        }

        var ordered = sceneLoadables.OrderBy(GetSceneLoadOrderKey).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            await ordered[i].OnSceneObjectLoadAsync(_sceneId, ct);
        }
        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].OnSceneObjectReady(_sceneId);
        }
    }

    private int GetSceneLoadOrderKey(ISceneLoadable loadable) => loadable.SceneLoadOrder;

    protected abstract void RegisterSceneServices();
    protected abstract UniTask PreloadSceneAssetsAsync();
    protected abstract UniTask SetupSceneActorsAsync();
    protected abstract UniTask SetupSceneUIAsync();
    protected abstract void OnSceneReadyCompleted();

    protected virtual void OnDestroy()
    {
        if (SceneLoadManager.Instance != null)
        {
            SceneLoadManager.Instance.UnregisterBootstrapper(this);
        }

        ServiceLocator.ClearSceneLocalServices();
        _isInitialized = false;
    }
}
