using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class MeteorProjectile : MonoBehaviour, IPoolable
{
    [SerializeField] private float _fallSpeed;
    [SerializeField] private float _explosionRadius = 4.0f;
    [SerializeField] private LayerMask _targetLayer;

    private GameObject _attacker;
    private Vector3 _targetPos;
    private float _damage;
    private float _poiseDamage;
    private bool _hasImpacted = false;
    private CancellationTokenSource _cts;

    public void OnSpawn()
    {
        _hasImpacted = false;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);
    }

    public void OnDespawn()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Launch(GameObject attacker, Vector3 targetFloorPos, float damage, float poiseDamage)
    {
        _attacker = attacker;
        _targetPos = targetFloorPos;
        _damage = damage;
        _poiseDamage = poiseDamage;

        FallAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid FallAsync(CancellationToken ct)
    {
        while (!_hasImpacted && !ct.IsCancellationRequested)
        {
            transform.position = Vector3.MoveTowards(transform.position, _targetPos, _fallSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, _targetPos) <= 0.5f)
            {
                Explode();
                break;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
    }

    private void Explode()
    {
        _hasImpacted = true;

        // 폭발 VFX 스폰
        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.PlayVFX(PoolType.VFX_MeteorExplosion, _targetPos, Quaternion.identity, null, SoundId.Dragon_Meteor_Impact);
        }
        else if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Spawn(PoolType.VFX_MeteorExplosion, _targetPos, Quaternion.identity);
            SoundManager.Instance?.PlaySFX(SoundId.Dragon_Meteor_Impact, _targetPos);
        }

        // 광역 대미지 판정
        Collider[] hits = Physics.OverlapSphere(_targetPos, _explosionRadius, _targetLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            if (_attacker != null && hits[i].transform.root == _attacker.transform.root) continue;

            var damageable = hits[i].GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                DamageContext ctx = new DamageContext(_attacker, _damage, _poiseDamage, hits[i].transform.position, Vector3.up, false, AttackImpactType.Heavy, 16.0f);
                damageable.ApplyDamage(in ctx);
            }
        }

        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Despawn(PoolType.MeteorProjectile, gameObject);
        }
    }
}