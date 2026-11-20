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
        if (_weaponHitbox == null) return;

        // _currentWeaponData가 아직 바인딩되지 않았다면 인벤토리 장착 무기에서 즉시 지연 조회
        if (_currentWeaponData == null && _owner != null && _owner.TryGetComponent(out PlayerInventoryHandler invHandler) &&
            invHandler.InventoryInstance != null)
        {
            WeaponItem equippedWeapon = invHandler.InventoryInstance.GetEquippedItem(EquipSlotType.MainWeapon) as WeaponItem;
            if (equippedWeapon != null)
            {
                _currentWeaponData = equippedWeapon.WeaponData;
            }
        }

        // 1. 단일 물리 베이스 공격력 연산 (STR / DEX 보정치 결합)
        float totalAR = CalculateCurrentAR();

        // 2. 모션값(MV) 적용
        float mv = _currentWeaponData != null ? _currentWeaponData.GetMotionValue(comboStep) : 100f;
        float basePoise = _currentWeaponData != null ? _currentWeaponData.BasePoiseDamage : 35f;

        float effectiveAR = totalAR * (mv / 100.0f);
        float effectivePoiseDamage = basePoise * (mv / 100.0f);

        // 3. 히트박스 개방 (타격 판정에 AR_eff 주입)
        _weaponHitbox.OpenHitBox(
        _owner.gameObject, effectiveAR,
        effectivePoiseDamage, isParryable: true, impactType: AttackImpactType.Light, knockbackForce: 0f);
    }

    public void CloseWeaponHitbox() => _weaponHitbox?.CloseHitBox();

    /// <summary>
    /// 처형(앞잡) 대미지 산출 및 단일 컨텍스트 주입
    /// </summary>
    public void ExecuteRiposteDamage(IDamageable target, Vector3 hitPoint)
    {
        if (target == null || _currentWeaponData == null) return;

        float totalAR = CalculateCurrentAR();
        float riposteAR = totalAR * (_currentWeaponData.Riposte_MV / 100.0f);

        DamageContext context = new DamageContext(_owner.gameObject, riposteAR, 0f, hitPoint, _owner.transform.forward,
            isParryable: false, impactType: AttackImpactType.Heavy, knockbackForce: 5.0f);

        target.ApplyDamage(in context);
    }

    public float CalculateCurrentAR()
    {
        PlayerInfo info = _owner.StatusController != null ? _owner.StatusController.PlayerInfoSource : null;
        int str = (info != null ? info.Strength : 10) + (_owner.StatusController != null ? _owner.StatusController.EquippedBonusStrength : 0);
        int dex = (info != null ? info.Dexterity : 10) + (_owner.StatusController != null ? _owner.StatusController.EquippedBonusDexterity : 0);
        float bonusAtk = _owner.StatusController != null ? _owner.StatusController.EquippedBonusAttackPower : 0f;

        if (_currentWeaponData == null)
        {
            return 100f + bonusAtk;
        }

        float baseDmg = _currentWeaponData.BaseDamage;
        float strBonus = baseDmg * _currentWeaponData.StrScaling * (str / 40.0f);
        float dexBonus = baseDmg * _currentWeaponData.DexScaling * (dex / 40.0f);

        return baseDmg + strBonus + dexBonus + bonusAtk;
    }

    /// <summary>
    /// 화톳불 레벨업 UI에서 근력/기량 투자 시 예상되는 물리 공격력(AR)을 사전 산출
    /// </summary>
    public float CalculatePreviewAR(int previewBaseStrength, int previewBaseDexterity)
    {
        if (_currentWeaponData == null) return 0f;

        int str = previewBaseStrength + (_owner.StatusController != null ? _owner.StatusController.EquippedBonusStrength : 0);
        int dex = previewBaseDexterity + (_owner.StatusController != null ? _owner.StatusController.EquippedBonusDexterity : 0);

        float baseDmg = _currentWeaponData.BaseDamage;
        float strBonus = baseDmg * _currentWeaponData.StrScaling * (str / 40.0f);
        float dexBonus = baseDmg * _currentWeaponData.DexScaling * (dex / 40.0f);
        float bonusAtk = _owner.StatusController != null ? _owner.StatusController.EquippedBonusAttackPower : 0f;

        return baseDmg + strBonus + dexBonus + bonusAtk;
    }
}