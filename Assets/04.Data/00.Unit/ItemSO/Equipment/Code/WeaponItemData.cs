using UnityEngine;

/// <summary>
/// 한손검 무기 데이터 SO
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponItemData", menuName = "Inventory/ItemData/WeaponItemData")]
public class WeaponItemData : EquipmentItemData
{
    [Header("공격력 및 보정 계수")]
    [SerializeField] private float _baseDamage = 110f;
    [SerializeField] private float _basePoiseDamage = 35f;
    [Tooltip("근력 보정 계수 (STR Scaling)")]
    [SerializeField] private float _strScaling = 0.8f;
    [Tooltip("기량 보정 계수 (DEX Scaling)")]
    [SerializeField] private float _dexScaling = 0.4f;

    [Header("모션값 (Motion Values, MV)")]
    [SerializeField] private float _lightAttack1_MV = 100f;
    [SerializeField] private float _lightAttack2_MV = 125f;
    [SerializeField] private float _lightAttack3_MV = 150f;
    [SerializeField] private float _riposte_MV = 320f;

    [Header("스태미나 소모")]
    [SerializeField] private float _attackStaminaCost = 18f;

    #region MyRegion
    public float BaseDamage => _baseDamage;
    public float BasePoiseDamage => _basePoiseDamage;
    public float StrScaling => _strScaling;
    public float DexScaling => _dexScaling;
    public float LightAttack1_MV => _lightAttack1_MV;
    public float LightAttack2_MV => _lightAttack2_MV;
    public float LightAttack3_MV => _lightAttack3_MV;
    public float Riposte_MV => _riposte_MV;
    public float AttackStaminaCost => _attackStaminaCost;
    #endregion

    [System.Serializable]
    public struct ComboTiming
    {
        public float HitboxStartNormalized;
        public float HitboxEndNormalized;
        public float EmergencyCancelNormalized;
        public float ComboAdvanceNormalized;
        public float MovementCancelNormalized;

        public ComboTiming(float start, float end, float emergency, float advance, float moveCancel)
        {
            HitboxStartNormalized = start;
            HitboxEndNormalized = end;
            EmergencyCancelNormalized = emergency;
            ComboAdvanceNormalized = advance;
            MovementCancelNormalized = moveCancel;
        }
    }
    [SerializeField]
    private ComboTiming[] _comboTimings = new ComboTiming[]
    {
        new ComboTiming(0.35f, 0.55f, 0.30f, 0.65f, 0.80f),
        new ComboTiming(0.30f, 0.50f, 0.25f, 0.60f, 0.78f),
        new ComboTiming(0.45f, 0.70f, 0.40f, 0.75f, 0.90f)
    };

    public override Item CreateItem() => new WeaponItem(this);
    public override StatScalingGrade StrScalingGrade => ConvertScalingToGrade(_strScaling);
    public override StatScalingGrade DexScalingGrade => ConvertScalingToGrade(_dexScaling);

    public override string GetEffectSummaryText()
    {
        return $"물리 공격력: {_baseDamage:F0}\n강인도 감쇄력: {_basePoiseDamage:F0}";
    }

    private StatScalingGrade ConvertScalingToGrade(float scaling)
    {
        if (scaling <= 0f) return StatScalingGrade.None;
        if (scaling < 0.3f) return StatScalingGrade.E;
        if (scaling < 0.6f) return StatScalingGrade.D;
        if (scaling < 0.9f) return StatScalingGrade.C;
        if (scaling < 1.2f) return StatScalingGrade.B;
        if (scaling < 1.5f) return StatScalingGrade.A;
        return StatScalingGrade.S;
    }

    /// <summary>
    /// 공격 타수에 따른 모션값(MV) 반환 
    /// </summary>
    public float GetMotionValue(int comboStep)
    {
        return comboStep switch
        {
            1 => _lightAttack1_MV,
            2 => _lightAttack2_MV,
            3 => _lightAttack3_MV,
            _ => _lightAttack1_MV
        };
    }

    /// <summary>
    /// 공격 타수에 따른 정규화 판정 타이밍 반환
    /// </summary>
    public ComboTiming GetComboTiming(int comboStep)
    {
        int index = Mathf.Clamp(comboStep - 1, 0, _comboTimings.Length - 1);
        return _comboTimings[index];
    }
}