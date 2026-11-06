using UnityEngine;

/// <summary>
/// WeaponSweepHitboxTest와 연동하여 보스 및 BT 에너미에게 DamageContextTest를 주입하는 플레이어 전용 순수 C# 전투 모듈
/// </summary>
public class PlayerCombatModule : UnitBaseModule
{
    private readonly PlayerController _owner;
    private readonly WeaponSweepHitbox _weaponHitbox;
    private WeaponItemData _currentWeaponData;

    public WeaponSweepHitbox WeaponHitbox => _weaponHitbox;
    public WeaponItemData CurrentWeaponData => _currentWeaponData;

    public PlayerCombatModule(PlayerController owner, WeaponSweepHitbox weaponHitbox)
    {
        _owner = owner;
        _weaponHitbox = weaponHitbox;
    }

    public void SetWeaponData(WeaponItemData weaponData) => _currentWeaponData = weaponData;

    /// <summary>
    /// 공격 타수(ComboStep)에 따른 최종 대미지와 강인도 감쇄량을 연산하여 테스트 무기 히트박스 개방
    /// </summary>
    public void OpenWeaponHitbox(int comboStep)
    {
        if (_weaponHitbox == null || _currentWeaponData == null) return;

        PlayerInfo playerInfo = _owner.StatusController != null ? _owner.StatusController.PlayerInfoSource : null;
        int str = playerInfo != null ? playerInfo.Strength : 10;
        int dex = playerInfo != null ? playerInfo.Dexterity : 10;

        // 1. 단일 물리 베이스 공격력 연산 (STR / DEX 보정치 결합)
        float baseDmg = _currentWeaponData.BaseDamage;
        float strBonus = baseDmg * _currentWeaponData.StrScaling * (str / 40.0f); // 소프트캡 곡선 적용
        float dexBonus = baseDmg * _currentWeaponData.DexScaling * (dex / 40.0f);
        float totalAR = baseDmg + strBonus + dexBonus;

        // 2. 모션값(MV) 적용
        float mv = _currentWeaponData.GetMotionValue(comboStep);
        float effectiveAR = totalAR * (mv / 100.0f);
        float effectivePoiseDamage = _currentWeaponData.BasePoiseDamage * (mv / 100.0f);

        // 3. 히트박스 개방 (타격 판정에 AR_eff 주입)
        _weaponHitbox.OpenHitBox(_owner.gameObject, effectiveAR, effectivePoiseDamage, isParryable: true,
            impactType: AttackImpactType.Light, knockbackForce: 0f);
    }

    public void CloseWeaponHitbox() => _weaponHitbox?.CloseHitBox();

    /// <summary>
    /// 처형(앞잡) 대미지 산출 및 단일 컨텍스트 주입
    /// </summary>
    public void ExecuteRiposteDamage(IDamageable target, Vector3 hitPoint)
    {
        if (target == null || _currentWeaponData == null) return;

        PlayerInfo playerInfo = _owner.StatusController != null ? _owner.StatusController.PlayerInfoSource : null;
        int str = playerInfo != null ? playerInfo.Strength : 10;
        int dex = playerInfo != null ? playerInfo.Dexterity : 10;

        float baseDmg = _currentWeaponData.BaseDamage;
        float totalAR = baseDmg + (baseDmg * _currentWeaponData.StrScaling * (str / 40.0f)) + (baseDmg * _currentWeaponData.DexScaling * (dex / 40.0f));

        // 앞잡 모션값 320 적용
        float riposteAR = totalAR * (_currentWeaponData.Riposte_MV / 100.0f);

        DamageContext context = new DamageContext(_owner.gameObject, riposteAR, 100.0f, hitPoint, _owner.transform.forward,
            isParryable: false, impactType: AttackImpactType.Heavy, knockbackForce: 5.0f);

        target.ApplyDamage(in context);
    }
}