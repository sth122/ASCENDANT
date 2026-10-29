using UnityEngine;

/// <summary>
/// WeaponSweepHitboxTest와 연동하여 보스 및 BT 에너미에게 DamageContextTest를 주입하는 플레이어 전용 순수 C# 전투 모듈
/// </summary>
public class PlayerCombatModule : UnitBaseModule
{
    private readonly UnitController _owner;
    private readonly WeaponSweepHitbox _weaponHitbox;

    private const float DefaultBaseDamage = 35.0f;
    private const float DefaultBasePoiseDamage = 25.0f;
    private const float DefaultRiposteMultiplier = 3.5f;

    public WeaponSweepHitbox WeaponHitbox => _weaponHitbox;

    public PlayerCombatModule(UnitController owner, WeaponSweepHitbox weaponHitbox)
    {
        _owner = owner;
        _weaponHitbox = weaponHitbox;
    }

    /// <summary>
    /// 공격 타수(ComboStep)에 따른 최종 대미지와 강인도 감쇄량을 연산하여 테스트 무기 히트박스 개방
    /// </summary>
    public void OpenWeaponHitbox(int comboStep)
    {
        if (_weaponHitbox == null) return;

        float comboMultiplier = 1.0f + Mathf.Max(0, comboStep - 1) * 0.3f;
        float finalDamage = DefaultBaseDamage * comboMultiplier;
        float finalPoiseDamage = DefaultBasePoiseDamage * comboMultiplier;

        _weaponHitbox.OpenHitBox(_owner.gameObject, finalDamage, finalPoiseDamage, isParryable: true,
            impactType: AttackImpactType.Light, knockbackForce: 0f);
    }

    public void CloseWeaponHitbox()
    {
        _weaponHitbox?.CloseHitBox();
    }

    /// <summary>
    /// 처형(앞잡) 대미지 산출 및 단일 컨텍스트 주입
    /// </summary>
    public void ExecuteRiposteDamage(IDamageable target, Vector3 hitPoint)
    {
        if (target == null) return;

        float finalRiposteDamage = DefaultBaseDamage * DefaultRiposteMultiplier;
        DamageContext context = new DamageContext(_owner.gameObject, finalRiposteDamage, 100.0f, hitPoint, _owner.transform.forward,
            isParryable: false, impactType: AttackImpactType.Heavy, knockbackForce: 5.0f);

        target.ApplyDamage(in context);
    }
}