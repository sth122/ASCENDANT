using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 궤적 히트박스 판정 컴포넌트
/// </summary>
public class WeaponSweepHitbox : MonoBehaviour
{
    [SerializeField] private Transform[] _hitPoints;
    [SerializeField] private float _hitRadius = 0.25f;
    [SerializeField] private LayerMask _targetLayer;

    private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];
    private readonly HashSet<IDamageable> _alreadyHitColliders = new HashSet<IDamageable>();
    private Vector3[] _previousPositions;
    private bool _isHitboxActive = false;
    private float _currentDamage = 0f;
    private float _currentPoiseDamage = 0f;
    private GameObject _attackerOwner;

    private void Awake()
    {
        if (_hitPoints == null || _hitPoints.Length == 0)
        {
            _hitPoints = new Transform[] { transform };
        }
        _previousPositions = new Vector3[_hitPoints.Length];
    }

    public void OpenHitBox(GameObject attacker, float damage, float poiseDamage)
    {
        _attackerOwner = attacker;
        _currentDamage = damage;
        _currentPoiseDamage = poiseDamage;
        _alreadyHitColliders.Clear();
        _isHitboxActive = true;

        for (int i = 0; i < _hitPoints.Length; i++)
        {
            if (_hitPoints[i] != null)
            {
                _previousPositions[i] = _hitPoints[i].position;
            }
        }
    }

    public void CloseHitBox()
    {
        _isHitboxActive = false;
        _alreadyHitColliders.Clear();
        _attackerOwner = null;
    }

    private void FixedUpdate()
    {
        if (!_isHitboxActive) return;

        // 타격 지점들의 이전 위치와 현재 위치 사이 공간 스위프 판정
        for (int i = 0; i < _hitPoints.Length; i++)
        {
            if (_hitPoints[i] == null) continue;

            Vector3 currentPos = _hitPoints[i].position;
            Vector3 prevPos = _previousPositions[i];
            Vector3 direction = currentPos - prevPos;
            float distance = direction.magnitude;

            if (distance > 0.0001f)
            {
                int hitCount = Physics.SphereCastNonAlloc( prevPos, _hitRadius, direction.normalized,
                    _hitBuffer, distance, _targetLayer,  QueryTriggerInteraction.Ignore);

                for (int h = 0; h < hitCount; h++)
                {
                    Collider hitCol = _hitBuffer[h].collider;

                    if (hitCol.gameObject == _attackerOwner) continue;

                    if(!hitCol.TryGetComponent<IDamageable>(out IDamageable damageable)) continue;

                    if(_alreadyHitColliders.Contains(damageable)) continue;
                    _alreadyHitColliders.Add(damageable);

                    DamageContext context = new DamageContext(_attackerOwner, _currentDamage, 
                        _currentPoiseDamage, _hitBuffer[h].point, _hitBuffer[h].normal);

                    damageable.ApplyDamage(context);
                }
            }

            _previousPositions[i] = currentPos;
        }
    }

    private void OnDrawGizmos()
    {
        if (_hitPoints == null) return;
        Gizmos.color = _isHitboxActive ? Color.red : Color.gray;
        for (int i = 0; i < _hitPoints.Length; i++)
        {
            if (_hitPoints[i] != null)
            {
                Gizmos.DrawWireSphere(_hitPoints[i].position, _hitRadius);
            }
        }
    }
}

