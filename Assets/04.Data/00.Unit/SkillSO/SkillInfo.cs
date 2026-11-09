[System.Serializable]
public struct SkillInfo
{
    [UnityEngine.Header("스킬 기본 식별")]
    public SkillId Id;
    public AnimStateType AnimState;
    public HitboxSlot TargetHitboxSlot;
    public AttackImpactType ImpactType;
    public float KnockbackForce;

    [UnityEngine.Header("페이즈 해금 조건")]
    [UnityEngine.Tooltip("스킬이 해금되는 최소 페이즈 (1: 1페이즈부터 상시 사용, 2: 2페이즈 전용)")]
    public int RequiredPhase; 

    [UnityEngine.Header("전투 밸런스 수치")]
    public float BaseDamage;
    public float BasePoiseDamage;
    public float Cooldown;
    public float Range;
    public bool IsParryable;

    [UnityEngine.Header("정규화 타임라인 (0.0 ~ 1.0)")]
    public float HitboxStartNormalized;
    public float HitboxEndNormalized;
    public float ActionEndNormalized;

    public SkillInfo(SkillId id, AnimStateType animState, HitboxSlot targetHitboxSlot, AttackImpactType impactType,
        float knockbackForce, int requiredPhase, float baseDamage, float basePoiseDamage, float cooldown, float range,
        bool isParryable, float hitboxStart = 0.25f, float hitboxEnd = 0.60f, float actionEnd = 0.90f)
    {
        Id = id;
        AnimState = animState;
        TargetHitboxSlot = targetHitboxSlot;
        ImpactType = impactType;
        KnockbackForce = knockbackForce;
        RequiredPhase = requiredPhase > 0 ? requiredPhase : 1;
        BaseDamage = baseDamage;
        BasePoiseDamage = basePoiseDamage;
        Cooldown = cooldown;
        Range = range;
        IsParryable = isParryable;
        HitboxStartNormalized = hitboxStart;
        HitboxEndNormalized = hitboxEnd;
        ActionEndNormalized = actionEnd;
    }
}