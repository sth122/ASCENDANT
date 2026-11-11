public abstract class EquipmentItemData : ItemData
{
    [UnityEngine.Header("장비 기본")]
    [UnityEngine.SerializeField] private EquipSlotType _equipSlot;
    [UnityEngine.SerializeField] private int _requiredStrength = 10;
    [UnityEngine.SerializeField] private int _requiredDexterity = 10;

    public EquipSlotType EquipSlot => _equipSlot;
    public int RequiredStrength => _requiredStrength;
    public int RequiredDexterity => _requiredDexterity;
}