using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class FireBombProjectile : MonoBehaviour, IPoolable
{
    [SerializeField] private float _explosionRadius = 2.5f;
    [SerializeField] private float _knockbackImpulse = 4.0f;
    [SerializeField] private LayerMask _targetLayer;

    private Rigidbody _rb;
    private GameObject _attackerUser;
    private float _damage;
    private float _poiseDamage;
    private bool _hasExploded = false;
    private readonly Collider[] _overlapBuffer = new Collider[16];

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void Launch(GameObject attacker, float damage, float poiseDamage, Vector3 velocity)
    {
        _attackerUser = attacker;
        _damage = damage;
        _poiseDamage = poiseDamage;
        _hasExploded = false;

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.linearVelocity = velocity;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasExploded) return;
        _hasExploded = true;

        Explode(collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
    }

    private void Explode(Vector3 explosionPoint)
    {
        // 1. 폭발 타격 VFX 스폰
        //if (ObjectPoolManager.Instance != null)
        //{
        //    ObjectPoolManager.Instance.Spawn(PoolType.HitVFX_Blunt, explosionPoint, Quaternion.identity);
        //}

        // 2. 반경 2.5m 내 피격체 탐색
        int count = Physics.OverlapSphereNonAlloc(explosionPoint, _explosionRadius, _overlapBuffer, _targetLayer);
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null || col.gameObject == _attackerUser) continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                Vector3 toTarget = col.transform.position - explosionPoint;
                Vector3 normal = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector3.up;

                DamageContext context = new DamageContext(
                    _attackerUser,
                    _damage,
                    _poiseDamage,
                    explosionPoint,
                    normal,
                    isParryable: false,
                    impactType: AttackImpactType.Heavy,
                    knockbackForce: _knockbackImpulse
                );

                damageable.ApplyDamage(in context);
            }
        }
        System.Array.Clear(_overlapBuffer, 0, count);

        // 3. 풀 자동 반환
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Despawn(PoolType.FireBomb, gameObject);
        }
    }

    public void OnSpawn()
    {
        _hasExploded = false;
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    public void OnDespawn()
    {
        _attackerUser = null;
        _hasExploded = false;
    }
}