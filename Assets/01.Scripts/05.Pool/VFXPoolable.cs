using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class VFXPoolable : MonoBehaviour, IPoolable
{
    [SerializeField] private PoolType _poolType;
    [SerializeField] private float _fallbackDuration = 2.0f;
    [SerializeField] private bool _autoDespawnByDuration = false;

    private ParticleSystem[] _allParticleSystems;
    private CancellationTokenSource _vfxCts;

    private void Awake()
    {
        _allParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    public void OnSpawn()
    {
        AsyncUtil.CancelAndDispose(ref _vfxCts);
        _vfxCts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);

        if (_allParticleSystems != null)
        {
            for (int i = 0; i < _allParticleSystems.Length; i++)
            {
                if (_allParticleSystems[i] == null) continue;
                _allParticleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _allParticleSystems[i].Clear(true);
                _allParticleSystems[i].time = 0f;
                _allParticleSystems[i].Play(true);
            }
        }

        if (_autoDespawnByDuration)
        {
            float duration = CalculateTotalDuration();
            WaitForDespawnAsync(duration, _vfxCts.Token).Forget();
        }
    }

    public void OnDespawn()
    {
        AsyncUtil.CancelAndDispose(ref _vfxCts);
        if (_allParticleSystems != null)
        {
            for (int i = 0; i < _allParticleSystems.Length; i++)
            {
                if (_allParticleSystems[i] == null) continue;
                _allParticleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private float CalculateTotalDuration()
    {
        if (_allParticleSystems == null || _allParticleSystems.Length == 0) return _fallbackDuration;
        float maxDuration = 0f;

        for (int i = 0; i < _allParticleSystems.Length; i++)
        {
            var ps = _allParticleSystems[i];
            if (ps == null) continue;

            var main = ps.main;
            float life = main.startLifetime.mode switch
            {
                ParticleSystemCurveMode.Constant => main.startLifetime.constant,
                ParticleSystemCurveMode.TwoConstants => main.startLifetime.constantMax,
                _ => 1.5f
            };
            float total = main.duration + life;
            if (total > maxDuration) maxDuration = total;
        }

        return maxDuration > 0.1f ? maxDuration : _fallbackDuration;
    }

    private async UniTaskVoid WaitForDespawnAsync(float delay, CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Despawn(_poolType, gameObject);
        }
    }

    private void OnDestroy() => AsyncUtil.CancelAndDispose(ref _vfxCts);
}