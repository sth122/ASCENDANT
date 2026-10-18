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
    private readonly Collider[] _overlapBuffer = new Collider[8];
    private readonly HashSet<IDamageable> _alreadyHitTargets = new HashSet<IDamageable>();
    private Vector3[] _previousPositions;
    private bool _isHitboxActive = false;
    private float _currentDamage = 0f;
    private float _currentPoiseDamage = 0f;
    private bool _isParryable = true;
    private GameObject _attackerOwner;

    private void Awake()
    {
        if (_hitPoints == null || _hitPoints.Length == 0)
        {
            _hitPoints = new Transform[] { transform };
        }
        _previousPositions = new Vector3[_hitPoints.Length];
    }

    public void OpenHitBox(GameObject attacker, float damage, float poiseDamage, bool isParryable = true)
    {
        _attackerOwner = attacker;
        _currentDamage = damage;
        _currentPoiseDamage = poiseDamage;
        _isParryable = isParryable;
        _alreadyHitTargets.Clear();
        _isHitboxActive = true;

        for (int i = 0; i < _hitPoints.Length; i++)
        {
            if (_hitPoints[i] != null)
            {
                _previousPositions[i] = _hitPoints[i].position;
            }
        }

        // 스윙 시작 프레임 근접 겹침(overlap) 검출)
        CheckPointBlankOverlap();
    }

    public void CloseHitBox()
    {
        _isHitboxActive = false;
        _alreadyHitTargets.Clear();
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

                    // 복합 콜라이더 부모 계층 탐색 (EnemyController는 최상위 오브젝트에 부착되어 있기 때문)
                    IDamageable damageable = hitCol.GetComponentInParent<IDamageable>();
                    if (damageable == null || _alreadyHitTargets.Contains(damageable)) continue;

                    _alreadyHitTargets.Add(damageable);

                    DamageContext context = new DamageContext(_attackerOwner, _currentDamage, 
                        _currentPoiseDamage, _hitBuffer[h].point, _hitBuffer[h].normal, _isParryable);

                    damageable.ApplyDamage(context);
                }
            }

            _previousPositions[i] = currentPos;
        }
    }

    private void CheckPointBlankOverlap()
    {
        for (int i = 0; i < _hitPoints.Length; i++)
        {
            if (_hitPoints[i] == null) continue;

            int overlapCount = Physics.OverlapSphereNonAlloc(_hitPoints[i].position, _hitRadius, _overlapBuffer, _targetLayer);
            for (int o = 0; o < overlapCount; o++)
            {
                Collider col = _overlapBuffer[o];
                if (col.gameObject == _attackerOwner) continue;

                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || _alreadyHitTargets.Contains(damageable)) continue;

                _alreadyHitTargets.Add(damageable);

                Vector3 hitPoint = col.ClosestPoint(_hitPoints[i].position);
                Vector3 hitNormal = (_hitPoints[i].position - hitPoint).normalized;

                DamageContext context = new DamageContext( _attackerOwner, _currentDamage, _currentPoiseDamage,
                    hitPoint, hitNormal, _isParryable);

                damageable.ApplyDamage(in context);
            }
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

