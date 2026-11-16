using UnityEngine;

[CreateAssetMenu(fileName = "NewAccessoryItemData", menuName = "Inventory/ItemData/AccessoryItemData")]
public class AccessoryItemData : EquipmentItemData
{
    [Header("악세서리 추가 스탯")]
    [SerializeField] private int _bonusStrength = 0;
    [SerializeField] private int _bonusDexterity = 0;
    [SerializeField] private float _bonusMaxHp = 0f;
    [SerializeField] private float _bonusMaxStamina = 0f;
    [SerializeField] private float _bonusAttackPower = 0f;
    [SerializeField] private float _bonusFlatDefense = 0f;

    public int BonusStrength => _bonusStrength;
    public int BonusDexterity => _bonusDexterity;
    public float BonusMaxHp => _bonusMaxHp;
    public float BonusMaxStamina => _bonusMaxStamina;
    public float BonusAttackPower => _bonusAttackPower;
    public float BonusFlatDefense => _bonusFlatDefense;

    public override Item CreateItem() => new AccessoryItem(this);

    public override string GetEffectSummaryText()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (_bonusStrength > 0) sb.AppendLine($"근력 +{_bonusStrength}");
        if (_bonusDexterity > 0) sb.AppendLine($"기량 +{_bonusDexterity}");
        if (_bonusMaxHp > 0) sb.AppendLine($"최대 HP +{_bonusMaxHp:F0}");
        if (_bonusMaxStamina > 0) sb.AppendLine($"최대 스태미나 +{_bonusMaxStamina:F0}");
        if (_bonusAttackPower > 0) sb.AppendLine($"물리 공격력 +{_bonusAttackPower:F0}");
        if (_bonusFlatDefense > 0) sb.AppendLine($"물리 방어력 +{_bonusFlatDefense:F0}");
        return sb.ToString().TrimEnd();
    }
}

public class AccessoryItem : EquipmentItem
{
    public AccessoryItemData AccessoryData => (AccessoryItemData)Data;
    public AccessoryItem(AccessoryItemData data) : base(data) { }
}