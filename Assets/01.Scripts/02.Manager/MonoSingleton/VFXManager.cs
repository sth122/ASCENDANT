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

    private bool _isHitstopActive = false;

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();
    }

    public UniTask OnSceneLoadCreate(SceneId scene) => UniTask.CompletedTask;
    public void Init(SceneId scene) => Util.Log($"[{scene}] VFXManager 준비 완료");
    public void OnSceneDestroy(SceneId scene) => _isHitstopActive = false;

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


    /// <summary>
    /// 소울라이크 전투 타격 연출 통합 (VFX + SFX + 미세 히트스톱)
    /// </summary>
    public void PlayCombatHitFeedback(Vector3 hitPoint, Vector3 hitNormal, AttackImpactType impactType, bool isBlocked = false, bool isParrySuccess = false)
    {
        Quaternion rot = hitNormal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(hitNormal) : Quaternion.identity;

        if (isParrySuccess)
        {
            //PlayVFX(PoolType.HitVFX_Parry, hitPoint, rot, null, SoundId.Shield_Parry_Success);
            TriggerHitstopAsync(0.065f).Forget();
            return;
        }

        if (isBlocked)
        {
            //PlayVFX(PoolType.HitVFX_Blunt, hitPoint, rot, null, SoundId.Shield_Guard_Impact);
            TriggerHitstopAsync(0.03f).Forget();
            return;
        }

        if (impactType == AttackImpactType.Heavy)
        {
            //PlayVFX(PoolType.HitVFX_Slash, hitPoint, rot, null, SoundId.Hit_Armor_Blunt);
            //PlayVFX(PoolType.BloodSplatter, hitPoint, rot);
            TriggerHitstopAsync(0.045f).Forget();
        }
        else
        {
            //PlayVFX(PoolType.HitVFX_Slash, hitPoint, rot, null, SoundId.Hit_Flesh_Slash);
            //PlayVFX(PoolType.BloodSplatter, hitPoint, rot);
        }
    }

    /// <summary>
    /// 0.03~0.07초의 짧은 타임스케일 정지로 검격 무게감 극대화
    /// </summary>
    public async UniTaskVoid TriggerHitstopAsync(float duration)
    {
        if (_isHitstopActive || (GameManager.Instance != null && GameManager.Instance.IsPaused)) return;

        _isHitstopActive = true;
        float prevScale = Time.timeScale;
        Time.timeScale = 0.05f;

        try
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(duration), ignoreTimeScale: true, cancellationToken: this.destroyCancellationToken);
        }
        finally
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPaused)
            {
                Time.timeScale = prevScale > 0f ? prevScale : 1.0f;
            }
            _isHitstopActive = false;
        }
    }
}