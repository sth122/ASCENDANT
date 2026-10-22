using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// 타격 파티클 및 환경 이펙트 전용 풀링 컴포넌트
/// UniTask 비동기 타이머를 통해 파티클 재생 완료 시 자동으로 풀에 반환
/// </summary>
public class VFXPoolable : MonoBehaviour, IPoolable
{
    [Header("Pool Identifier")]
    [SerializeField] private PoolType _poolType;

    [Header("Particle Settings")]
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private float _fallbackDuration = 1.5f;

    private CancellationTokenSource _vfxCts;

    private void Awake()
    {
        if (_particleSystem == null)
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }
    }

    public void OnSpawn()
    {
        ResetVfxToken();
        _vfxCts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);

        if (_particleSystem != null)
        {
            _particleSystem.Clear();
            _particleSystem.Play();
        }

        float duration = _particleSystem != null ? _particleSystem.main.duration : _fallbackDuration;
        WaitForDespawnAsync(duration, _vfxCts.Token).Forget();
    }

    public void OnDespawn()
    {
        ResetVfxToken();

        if (_particleSystem != null)
        {
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private async UniTaskVoid WaitForDespawnAsync(float delay, CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay((int)(delay * 1000f), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        ObjectPoolManager.Instance.Despawn(_poolType, gameObject);
    }

    private void ResetVfxToken()
    {
        if (_vfxCts != null)
        {
            _vfxCts.Cancel();
            _vfxCts.Dispose();
            _vfxCts = null;
        }
    }

    private void OnDestroy()
    {
        ResetVfxToken();
    }
}