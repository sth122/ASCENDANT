using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class BossMeteorShower : MonoBehaviour, IPoolable
{
    [Header("Meteor Shower Settings")]
    [SerializeField] private float _radius = 8.0f;
    [SerializeField] private int _dropCount = 8;
    [SerializeField] private float _delayBetweenDrops = 0.35f;
    [SerializeField] private float _meteorExplosionRadius = 2.5f;
    [SerializeField] private float _meteorDamage = 45.0f;
    [SerializeField] private float _meteorPoiseDamage = 40.0f;
    [SerializeField] private float _knockbackForce = 16.0f;

    [Header("Target Layer Mask")]
    [SerializeField] private LayerMask _targetLayer;

    private readonly Collider[] _hitOverlapBuffer = new Collider[8];
    private CancellationTokenSource _showerCts;

    public void OnSpawn()
    {
        AsyncUtil.CancelAndDispose(ref _showerCts);
        _showerCts = new CancellationTokenSource();
        ExecuteMeteorShowerLoopAsync(_showerCts.Token).Forget();
    }
    public void OnDespawn()
    {
        AsyncUtil.CancelAndDispose(ref _showerCts);
    }

    private async UniTaskVoid ExecuteMeteorShowerLoopAsync(CancellationToken token)
    {
        try
        {
            for (int i = 0; i < _dropCount; i++)
            {
                if (token.IsCancellationRequested) return;

                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * _radius;
                Vector3 targetDropPos = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

                ExecuteSingleMeteorExplosionAsync(targetDropPos, token).Forget();

                await UniTask.Delay(TimeSpan.FromSeconds(_delayBetweenDrops), cancellationToken: token);
            }

            // 모든 낙하 완료 후 2초간 여운 이펙트 대기
            await UniTask.Delay(TimeSpan.FromSeconds(2.0f), cancellationToken: token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            // 오브젝트 풀에 안전하게 반환
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Despawn(PoolType.MeteorShower, gameObject);
            }
        }
    }

    private async UniTaskVoid ExecuteSingleMeteorExplosionAsync(Vector3 explosionPos, CancellationToken token)
    {
        // 낙하 경고 딜레이 (0.5초)
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // NonAlloc 구체 오버랩 검출 (GC 할당 0)
        int hitCount = Physics.OverlapSphereNonAlloc(
            explosionPos,
            _meteorExplosionRadius,
            _hitOverlapBuffer,
            _targetLayer,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hitOverlapBuffer[i];
            if (col == null) continue;

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                DamageContext ctx = new DamageContext(gameObject, _meteorDamage, _meteorPoiseDamage, explosionPos, Vector3.up,
                    isParryable: false, impactType: AttackImpactType.Heavy, knockbackForce: _knockbackForce);

                target.ApplyDamage(in ctx);
            }
        }

        Array.Clear(_hitOverlapBuffer, 0, hitCount);
    }

    private void OnDisable()
    {
        OnDespawn();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}