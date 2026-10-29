using System.Collections.Generic;
using UnityEngine;

public class FireGroundHazard : MonoBehaviour
{
    [SerializeField] private float _damagePerTick = 15.0f;
    [SerializeField] private float _tickInterval = 0.5f;
    [SerializeField] private float _lifeTime = 5.0f;

    private readonly Dictionary<IDamageableTest, float> _nextTickTimes = new Dictionary<IDamageableTest, float>();
    private float _expireTime;

    private void Start() => _expireTime = Time.time + _lifeTime;

    private void Update()
    {
        if (Time.time >= _expireTime) Destroy(gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        IDamageableTest target = other.GetComponentInParent<IDamageableTest>();
        if (target == null || !other.CompareTag("Player")) return;

        if (!_nextTickTimes.TryGetValue(target, out float nextTime) || Time.time >= nextTime)
        {
            _nextTickTimes[target] = Time.time + _tickInterval;

            DamageContext dotContext = new DamageContext(
                gameObject,
                _damagePerTick,
                poiseDamage: 0f,
                other.ClosestPoint(transform.position),
                Vector3.up,
                isParryable: false,
                impactType: AttackImpactType.DoT,
                knockbackForce: 0f
            );

            target.ApplyDamage(in dotContext);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        IDamageableTest target = other.GetComponentInParent<IDamageableTest>();
        if (target != null) _nextTickTimes.Remove(target);
    }
}