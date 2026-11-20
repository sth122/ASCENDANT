using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Util = DebugLogger<VFXManager>;

/// <summary>
/// ObjectPoolManager 위의 시각 연출 통합 매니저
/// VFX 스폰, 소켓 추적, 타격 히트스톱(역경직) 및 SoundManager 동시 트리거를 담당합니다.
/// </summary>
public class VFXManager : MonoSingleton<VFXManager>, ILoadable
{
    public int LoadOrder => 6;

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();
    }

    public UniTask OnSceneLoadCreate(SceneId scene) => UniTask.CompletedTask;
    public void Init(SceneId scene) => Util.Log($"[{scene}] VFXManager 준비 완료");
    public void OnSceneDestroy(SceneId scene) { }
    /// <summary>
    /// 풀링된 VFX 재생 + 선택적 동시 SFX 재생
    /// </summary>
    public GameObject PlayVFX(PoolType vfxType, Vector3 position, Quaternion rotation,
        Transform followParent = null, SoundId pairedSfx = SoundId.None)
    {
        if (vfxType == PoolType.None || ObjectPoolManager.Instance == null) return null;

        GameObject vfxGo = ObjectPoolManager.Instance.Spawn(vfxType, position, rotation, followParent);

        if (pairedSfx != SoundId.None && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(pairedSfx, position);
        }

        return vfxGo;
    }

    public void DespawnVFX(PoolType vfxType, GameObject vfxGo)
    {
        if (vfxGo == null || vfxType == PoolType.None) return;
        vfxGo.transform.SetParent(null);
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Despawn(vfxType, vfxGo);
        }
    }
}