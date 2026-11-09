using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 UnitId를 단일 키로 하여 기본 평타와 스킬셋을 통합 관리하는 전역 데이터베이스
/// </summary>
[CreateAssetMenu(fileName = "UnitCombatDatabaseSO", menuName = "Unit/Combat/UnitCombatDatabaseSO")]
public class UnitCombatDatabaseSO : KeyedDataSO<UnitId, UnitCombatPackage>
{
    private readonly Dictionary<UnitId, Dictionary<AnimStateType, EnemyAttackData>> _attackCache
        = new Dictionary<UnitId, Dictionary<AnimStateType, EnemyAttackData>>();

    public override void Init()
    {
        base.Init();
        _attackCache.Clear();

        foreach (var entry in _dictionary)
        {
            UnitId unitId = entry.Key;
            var package = entry.Value;

            if (package.BasicAttacks == null) continue;

            var attackDict = new Dictionary<AnimStateType, EnemyAttackData>();
            for (int i = 0; i < package.BasicAttacks.Count; i++)
            {
                var attack = package.BasicAttacks[i];
                attackDict[attack.AnimState] = attack.AttackData;
            }
            _attackCache[unitId] = attackDict;
        }
    }

    public bool TryGetBasicAttacks(UnitId unitId, out Dictionary<AnimStateType, EnemyAttackData> attacks)
    {
        return _attackCache.TryGetValue(unitId, out attacks);
    }

    public bool TryGetSkills(UnitId unitId, out List<BossSkillDataSO> skills)
    {
        if (TryGetData(unitId, out UnitCombatPackage package))
        {
            skills = package.Skills;
            return skills != null && skills.Count > 0;
        }
        skills = null;
        return false;
    }
}