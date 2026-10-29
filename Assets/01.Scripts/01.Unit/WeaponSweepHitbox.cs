using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BoxCast 및 OverlapBox 기반의 궤적 히트박스 판정 컴포넌트
/// 검의 형태 및 대형 몬스터의 직사각형 타격 범위에 최적화
/// </summary>
public class WeaponSweepHitbox : MonoBehaviour
{
    [Header("Box Dimensions (Half Extents)")]
    [Tooltip("중심 기준 가로/세로/깊이의 절반 크기 (예: 검날 폭 0.2, 두께 0.1, 길이 0.8)")]
    [SerializeField] private Vector3 _boxHalfExtents = new Vector3(0.3f, 0.3f, 0.8f);

    [Header("Hit Target Settings")]
    [SerializeField] private Transform[] _hitPoints;
    [SerializeField] private LayerMask _targetLayer;

    private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];
    private readonly Collider[] _overlapBuffer = new Collider[16];
    private readonly HashSet<IDamageable> _alreadyHitTargets = new HashSet<IDamageable>();

    private Vector3[] _previousPositions;
    private Quaternion[] _previousRotations;
    private bool _isHitboxActive = false;

    // 공격 사양 캐시
    private GameObject _attackerOwner;
    private GameObject _attackerRoot;
    private float _currentDamage = 0f;
    private float _currentPoiseDamage = 0f;
    private bool _isParryable = true;
    private AttackImpactType _currentImpactType = AttackImpactType.Light;
    private float _currentKnockbackForce = 0f;

    private void Awake()
    {
        if (_hitPoints == null || _hitPoints.Length == 0)
        {
            _hitPoints = new Transform[] { transform };
        }
        _previousPositions = new Vector3[_hitPoints.Length];
        _previousRotations = new Quaternion[_hitPoints.Length];
    }

    public void OpenHitBox(GameObject attacker, float damage, float poiseDamage, bool isParryable = true,
        AttackImpactType impactType = AttackImpactType.Light, float knockbackForce = 0f)
    {
        _attackerOwner = attacker;
        _attackerRoot = attacker != null ? attacker.transform.root.gameObject : null;
        _currentDamage = damage;
        _currentPoiseDamage = poiseDamage;
        _isParryable = isParryable;
        _currentImpactType = impactType;
        _currentKnockbackForce = knockbackForce;

        _alreadyHitTargets.Clear();
        _isHitboxActive = true;

        for (int i = 0; i < _hitPoints.Length; i++)
        {
            if (_hitPoints[i] != null)
            {
                _previousPositions[i] = _hitPoints[i].position;
                _previousRotations[i] = _hitPoints[i].rotation;
            }
        }

        CheckPointBlankOverlap();
    }

    public void CloseHitBox()
    {
        _isHitboxActive = false;
        _alreadyHitTargets.Clear();
        _attackerOwner = null;
        _attackerRoot = null;
    }

    private void LateUpdate()
    {
        if (!_isHitboxActive) return;

        for (int i = 0; i < _hitPoints.Length; i++)
        {
            if (_hitPoints[i] == null) continue;

            Vector3 currentPos = _hitPoints[i].position;
            Quaternion currentRot = _hitPoints[i].rotation;
            Vector3 prevPos = _previousPositions[i];
            Vector3 delta = currentPos - prevPos;
            float distance = delta.magnitude;

            if (distance > 0.0001f)
            {
                // 이전 프레임 위치에서 현재 위치 방향으로 박스 스윕 검출
                int hitCount = Physics.BoxCastNonAlloc(prevPos, _boxHalfExtents, delta.normalized, _hitBuffer,
                    currentRot, distance, _targetLayer, QueryTriggerInteraction.Ignore);

                for (int h = 0; h < hitCount; h++)
                {
                    Collider hitCol = _hitBuffer[h].collider;

                    // [자해 방지] 피격 개체의 최상위 부모가 공격자 루트와 일치하면 무시
                    if (hitCol.transform.root.gameObject == _attackerRoot) continue;

                    IDamageable damageable = hitCol.GetComponentInParent<IDamageable>();
                    if (damageable == null || _alreadyHitTargets.Contains(damageable)) continue;

                    _alreadyHitTargets.Add(damageable);

                    DamageContext context = new DamageContext(_attackerOwner, _currentDamage, _currentPoiseDamage, 
                        _hitBuffer[h].point, _hitBuffer[h].normal, _isParryable, _currentImpactType, _currentKnockbackForce);

                    damageable.ApplyDamage(in context);
                }
            }

            _previousPositions[i] = currentPos;
            _previousRotations[i] = currentRot;
        }
    }

    private void CheckPointBlankOverlap()
    {
        for (int i = 0; i < _hitPoints.Length; i++)
        {
            if (_hitPoints[i] == null) continue;

            int overlapCount = Physics.OverlapBoxNonAlloc(_hitPoints[i].position, _boxHalfExtents, _overlapBuffer,
                _hitPoints[i].rotation, _targetLayer, QueryTriggerInteraction.Ignore);

            for (int o = 0; o < overlapCount; o++)
            {
                Collider col = _overlapBuffer[o];
                if (col.transform.root.gameObject == _attackerRoot) continue;

                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || _alreadyHitTargets.Contains(damageable)) continue;

                _alreadyHitTargets.Add(damageable);

                Vector3 hitPoint = col.ClosestPoint(_hitPoints[i].position);
                Vector3 normalDiff = _hitPoints[i].position - hitPoint;
                Vector3 hitNormal = normalDiff.sqrMagnitude > 0.0001f ? normalDiff.normalized : Vector3.up;

                DamageContext context = new DamageContext(_attackerOwner, _currentDamage, _currentPoiseDamage, hitPoint,
                    hitNormal, _isParryable, _currentImpactType, _currentKnockbackForce);

                damageable.ApplyDamage(in context);
            }
        }
    }

    private void OnDisable() => CloseHitBox();

    private void OnDrawGizmos()
    {
        if (_hitPoints == null) return;
        Gizmos.color = _isHitboxActive ? new Color(1f, 0f, 0f, 0.7f) : new Color(0.5f, 0.5f, 0.5f, 0.3f);

        for (int i = 0; i < _hitPoints.Length; i++)
        {
            if (_hitPoints[i] == null) continue;

            Matrix4x4 rotationMatrix = Matrix4x4.TRS(_hitPoints[i].position, _hitPoints[i].rotation, Vector3.one);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawWireCube(Vector3.zero, _boxHalfExtents * 2f);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}