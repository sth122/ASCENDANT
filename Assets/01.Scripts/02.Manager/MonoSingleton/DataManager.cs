using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using Util = DebugLogger<DataManager>;

/// <summary>
/// Addressables 라벨("Static_Data")을 통해 모든 전역 기획 SO를 비동기 일괄 적재하고,
/// 타입별 캐시 및 O(1) 제네릭 조회를 제공하는 중앙 데이터 매니저
/// </summary>
public class DataManager : MonoSingleton<DataManager>, ILoadable
{
    public int LoadOrder => 1;

    // 타입 기반 단일 SO 캐시
    private readonly Dictionary<Type, ScriptableObject> _singleDataCache = new Dictionary<Type, ScriptableObject>();

    private bool _isDataLoaded = false;
    private UniTaskCompletionSource _loadCompletionSource;
    public bool IsDataLoaded => _isDataLoaded;

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();
        SceneLoadManager.Instance.RegisterLoadable(this);
    }

    #region ILoadable 구현부
    /// <summary>
    /// Addressables 'Static_Data' 라벨을 통해 등록된 모든 기획 ScriptableObject를 일괄 적재
    /// </summary>
    public async UniTask OnSceneLoadCreate(SceneId scene)
    {
        await EnsureDataLoadedAsync(this.destroyCancellationToken);
    }

    public void Init(SceneId scene) => Util.Log($"DataManager 준비 완료 (Current Scene: {scene})");

    public void OnSceneDestroy(SceneId scene)
    {
        // 전역 데이터이므로 씬 전환 시 언로드하지 않고 세션 동안 영구 보존
    }
    #endregion

    public async UniTask EnsureDataLoadedAsync(System.Threading.CancellationToken ct = default)
    {
        if (_isDataLoaded) return;

        // 이미 다른 객체에 의해 로딩이 진행 중인 경우 완료 태스크만 대기
        if (_loadCompletionSource != null)
        {
            await _loadCompletionSource.Task;
            return;
        }

        _loadCompletionSource = new UniTaskCompletionSource();
        const string dataLabel = "Static_Data";

        Util.Log($"전역 기획 데이터 일괄 로드 시작 (Label: {dataLabel})");

        try
        {
            IList<ScriptableObject> loadedAssets = await AssetsManager.Instance.LoadAssetsByLabelAsync<ScriptableObject>(
                dataLabel, ct, isGlobal: true
            );

            if (loadedAssets != null && loadedAssets.Count > 0)
            {
                for (int i = 0; i < loadedAssets.Count; i++)
                {
                    RegisterAsset(loadedAssets[i]);
                }
                Util.Log($"전역 기획 데이터 총 {loadedAssets.Count}종 캐싱 및 초기화 완료");
            }
            else
            {
                Util.LogError($"로드된 기획 데이터가 없습니다. Addressables 라벨({dataLabel})을 확인하세요.");
            }

            _isDataLoaded = true;
            _loadCompletionSource.TrySetResult();
        }
        catch (Exception ex)
        {
            Util.LogError($"데이터베이스 비동기 적재 실패: {ex.Message}");
            _loadCompletionSource.TrySetException(ex);
        }
    }

    /// <summary>
    /// 로드된 SO를 타입별로 캐싱하고, 테이블 형태의 SO인 경우 내부 Dictionary를 자동 빌드
    /// </summary>
    private void RegisterAsset(ScriptableObject asset)
    {
        if (asset == null) return;
        Type assetType = asset.GetType();

        _singleDataCache[assetType] = asset;

        // IInitable 인터페이스 다형성을 통한 무결성 일괄 초기화
        if (asset is IInitable initable)
        {
            initable.Init();
        }

        Util.Log($"[SO 캐시 등록 완료] Type: {assetType.Name}");
    }

    #region 제네릭 Getter API
    /// <summary>
    /// 특정 타입의 ScriptableObject 데이터 에셋을 단일 조회
    /// 사용 예: DataManager.Instance.GetSingle<EnemyStatusSO>();
    /// </summary>
    public T GetSingle<T>() where T : ScriptableObject
    {
        Type targetType = typeof(T);
        if (_singleDataCache.TryGetValue(targetType, out ScriptableObject asset))
        {
            return asset as T;
        }

        Util.LogError($"요청한 기획 데이터를 찾을 수 없습니다: {targetType.Name}");
        return null;
    }

    /// <summary>
    /// 특정 타입의 데이터 존재 여부를 안전하게 확인하고 반환
    /// </summary>
    public bool TryGetSingle<T>(out T asset) where T : ScriptableObject
    {
        Type targetType = typeof(T);
        if (_singleDataCache.TryGetValue(targetType, out ScriptableObject foundAsset))
        {
            asset = foundAsset as T;
            return true;
        }

        asset = null;
        return false;
    }
    #endregion
}