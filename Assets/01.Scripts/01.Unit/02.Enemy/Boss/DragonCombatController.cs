using UnityEngine;

/// <summary>
/// 드래곤 전용 전투 제어 컴포넌트
/// EarthShatter 지면 파쇄 및 착지 충격파를 처리합니다.
/// </summary>
public class DragonCombatController : EnemyCombatController
{
    [Header("Dragon EarthShatter Settings")]
    [SerializeField] private float _shatterRadius = 4.0f;
    [SerializeField] private float _shatterMaxHeight = 1.2f; // 1.2m 이상 점프 시 회피 가능
    [SerializeField] private LayerMask _obstacleLayer;

    public void TriggerEarthShatter(float damage, float poiseDamage)
    {
        Vector3 spawnPos = RightHandSocket.position;
        if (Physics.Raycast(spawnPos + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 3.5f, LayerMask.GetMask("Ground", "Default")))
        {
            spawnPos = hit.point;
        }

        // VFX 스폰
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Spawn(PoolType.VFX_EarthShatter, spawnPos, Quaternion.identity);
        }

        // 지면 충격파 연산 (NonAlloc + 점프 회피 + 지형 차폐)
        ExecuteAreaDamageNonAlloc(spawnPos, _shatterRadius, _shatterMaxHeight, damage, poiseDamage,
            AttackImpactType.Heavy, 3f, _owner.TargetLayer, _obstacleLayer);
    }
}