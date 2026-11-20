// UnitCombatPackage.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct UnitCombatPackage
{
    [Header("기본 평타 공격 목록")]
    public List<EnemyAttackEntry> BasicAttacks;

    [Header("특수 스킬 목록 (엘리트/보스 전용)")]
    public List<BossSkillDataSO> Skills;
}

[Serializable]
public struct EnemyAttackEntry
{
    public AnimStateType AnimState;
    public EnemyAttackData AttackData;
}