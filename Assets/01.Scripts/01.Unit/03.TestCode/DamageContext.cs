/// <summary>
/// 공격 및 타격 시 전달되는 순수 데이터 컨텍스트 구조체
/// </summary>
public readonly struct DamageContext
{
    public readonly UnityEngine.GameObject Attacker;
    public readonly float Damage;
    public readonly float PoiseDamage;
    public readonly UnityEngine.Vector3 HitPoint;
    public readonly UnityEngine.Vector3 HitNormal;
    public readonly bool IsParryable;
    public readonly AttackImpactType ImpactType;
    public readonly float KnockbackForce;

    public DamageContext(UnityEngine.GameObject attacker, float damage, float poiseDamage, UnityEngine.Vector3 hitPoint, UnityEngine.Vector3 hitNormal, 
        bool isParryable = true, AttackImpactType impactType = AttackImpactType.Light, float knockbackForce = 0f)
    {
        Attacker = attacker;
        Damage = damage;
        PoiseDamage = poiseDamage;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        IsParryable = isParryable;
        ImpactType = impactType;
        KnockbackForce = knockbackForce;
    }
}