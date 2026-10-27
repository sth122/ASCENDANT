using UnityEngine;

/// <summary>
/// 플레이어의 기본 공격력, 콤보 배율 계산, 무기 스윕 히트박스 개폐 및 앞잡(Riposte) 판정을 전담하는 순수 C# 전투 모듈
/// </summary>
public class PlayerCombatModule : UnitBaseModule
{
    private readonly PlayerController _owner;
    private readonly WeaponSweepHitbox _weaponHitbox;

    // SO 만들기 전 임시 데이터
    private const float DefaultBaseDamage = 35.0f;
    private const float DefaultBasePoiseDamage = 25.0f;
    private const float DefaultRiposteMultiplier = 3.5f;

    public WeaponSweepHitbox WeaponHitbox => _weaponHitbox;

    public PlayerCombatModule(PlayerController owner, WeaponSweepHitbox weaponSweepHitbox)
    {
        this._owner = owner;
        this._weaponHitbox = weaponSweepHitbox;
    }

    /// <summary>
    /// 공격 타수(ComboStep)에 따른 최종 대미지와 강인도 감쇄량을 연산하여 무기 히트박스 활성화
    /// </summary>
    public void OpenWeaponHitbox(int comboStep)
    {
        if (_weaponHitbox == null) return;

        float comboMultiplier = 1.0f + Mathf.Max(0, comboStep - 1) * 0.3f;
        float finalDamage = DefaultBaseDamage * comboMultiplier;
        float finalPoiseDamage = DefaultBasePoiseDamage * comboMultiplier;

        _weaponHitbox.OpenHitBox(_owner.gameObject, finalDamage, finalPoiseDamage, isParryable: true);
    }

    public void CloseWeaponHitbox() => _weaponHitbox?.CloseHitBox();

    /// <summary>
    /// 처형(앞잡) 대미지 산출 및 단일 컨텍스트 주입
    /// </summary>
    public void ExecuteRiposteDamage(IDamageable target, Vector3 hitPoint)
    {
        if (target == null) return;

        float finalRiposteDamage = DefaultBaseDamage * DefaultRiposteMultiplier;
        DamageContext context = new DamageContext(_owner.gameObject, finalRiposteDamage,
            100.0f, hitPoint, _owner.transform.forward, isParryable: false
        );

        target.ApplyDamage(context);
    }
}
