using System;
using UnityEngine;

[Serializable]
public abstract class Blackboard
{

}

/// <summary>
/// 보스 AI 노드들이 상태를 공유하고 런타임 타임스탬프를 통제하는 메모리 저장소
/// </summary>
[Serializable]
public class BossBlackboard : Blackboard
{
    [Header("Gimmick & Balance Settings")]
    public float SkillCycleInterval = 15.0f;
    public float Phase2HpRatio = 0.30f;
    public float Phase2HealRatio = 0.10f;
    public float Phase2StatBuffMultiplier = 1.30f;
    public float SkillCooldownResetChance = 0.30f;

    #region Runtime Flags & Timestamps
    [NonSerialized] public bool IsPhase2 = false;
    [NonSerialized] public bool IsInvincible = false;
    [NonSerialized] public bool IsActionExecuting = false;
    [NonSerialized] public bool IsBasicAttacking = false;
    [NonSerialized] public bool HasExecutedPhase2Entry = false;
    [NonSerialized] public bool IsSkillPending = false;

    // 타임스탬프 기반 주기 관리 (Update 연산 제거)
    [NonSerialized] public float NextSkillCycleTime = 0f;
    #endregion

    public void ResetSkillCycle()
    {
        NextSkillCycleTime = Time.time + SkillCycleInterval;
        IsSkillPending = false;
    }

    public void ForceReadySkillCycle()
    {
        NextSkillCycleTime = Time.time;
    }
}