/// <summary>
/// 장비 아이템 런타임 인스턴스
/// </summary>
public abstract class EquipmentItem : Item
{
    public EquipmentItemData EquipData => (EquipmentItemData)Data;
    public EquipmentItem(EquipmentItemData data) : base(data) { }
}

public class WeaponItem : EquipmentItem
{
    public WeaponItemData WeaponData => (WeaponItemData)Data;
    public WeaponItem(WeaponItemData data) : base(data) { }
}

public class ArmorItem : EquipmentItem
{
    public ArmorItemData ArmorData => (ArmorItemData)Data;
    public ArmorItem(ArmorItemData data) : base(data) { }
}