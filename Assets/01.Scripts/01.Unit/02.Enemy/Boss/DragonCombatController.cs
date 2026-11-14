using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드래곤 전용 전투 제어 컴포넌트
/// 오른쪽 앞발 기준 전방 3갈래 부채꼴 EarthShatter 지면 파쇄를 처리합니다.
/// </summary>
public class DragonCombatController : EnemyCombatController
{
    [Header("Dragon EarthShatter Settings")]
    [SerializeField] private float _shatterRadius = 2.8f;
    [SerializeField] private float _shatterForwardDistance = 4.5f;
    [SerializeField] private float _shatterSpreadAngle = 28.0f;
    [SerializeField] private float _shatterMaxHeight = 1.2f; // 1.2m 이상 점프 시 회피 가능
    [SerializeField] private LayerMask _obstacleLayer;

    private static readonly Collider[] _branchHitBuffer = new Collider[16];
    private readonly HashSet<IDamageable> _damagedTargetsInShatter = new HashSet<IDamageable>();

    public void TriggerEarthShatter(float damage, float poiseDamage)
    {
        Vector3 originPos = RightHandSocket.position;
        if (Physics.Raycast(originPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit groundHit, 5.0f, LayerMask.GetMask("Ground", "Default")))
        {
            originPos = groundHit.point;
        }

        // 드래곤 본체의 수평 정면 벡터 기준
        Vector3 baseForward = _owner != null ? _owner.transform.forward : transform.forward;
        baseForward.y = 0f;
        if (baseForward.sqrMagnitude < 0.001f) baseForward = Vector3.forward;
        baseForward.Normalize();

        SoundManager.Instance?.PlaySFX(SoundId.Dragon_EarthShatter, originPos);
        _damagedTargetsInShatter.Clear();

        // 좌(-Angle), 중앙(0), 우(+Angle) 3갈래 부채꼴 전개
        float[] branchAngles = { -_shatterSpreadAngle, 0f, _shatterSpreadAngle };

        for (int b = 0; b < branchAngles.Length; b++)
        {
            Quaternion branchRot = Quaternion.AngleAxis(branchAngles[b], Vector3.up);
            Vector3 branchDir = branchRot * baseForward;
            Vector3 branchPos = originPos + branchDir * _shatterForwardDistance;

            // 각 방향의 지면 높이 보정
            if (Physics.Raycast(branchPos + Vector3.up * 2.0f, Vector3.down, out RaycastHit branchGround, 5.0f, LayerMask.GetMask("Ground", "Default")))
            {
                branchPos = branchGround.point;
            }

            Quaternion vfxRot = Quaternion.LookRotation(branchDir);
            if (VFXManager.Instance != null)
            {
                VFXManager.Instance.PlayVFX(PoolType.VFX_EarthShatter, branchPos, vfxRot);
            }
            else if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Spawn(PoolType.VFX_EarthShatter, branchPos, vfxRot);
            }

            ApplyBranchDamageNonAlloc(branchPos, damage, poiseDamage);
        }

        _damagedTargetsInShatter.Clear();
    }

    private void ApplyBranchDamageNonAlloc(Vector3 center, float damage, float poiseDamage)
    {
        LayerMask targetLayer = _owner != null ? _owner.TargetLayer : LayerMask.GetMask("Player");
        int count = Physics.OverlapSphereNonAlloc(center, _shatterRadius, _branchHitBuffer, targetLayer);

        for (int i = 0; i < count; i++)
        {
            Collider col = _branchHitBuffer[i];
            if (col == null || col.transform.root == transform.root) continue;

            // 점프 회피 높이 판정
            if (Mathf.Abs(col.transform.position.y - center.y) > _shatterMaxHeight) continue;

            // 지형 엄폐물 차폐 판정
            if (Physics.Linecast(center + Vector3.up * 0.3f, col.bounds.center, _obstacleLayer)) continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null || _damagedTargetsInShatter.Contains(damageable)) continue;

            _damagedTargetsInShatter.Add(damageable);
            DamageContext ctx = new DamageContext(gameObject, damage, poiseDamage, col.transform.position, Vector3.up, false, AttackImpactType.Heavy, 3.0f);
            damageable.ApplyDamage(in ctx);
        }

        System.Array.Clear(_branchHitBuffer, 0, count);
    }
}