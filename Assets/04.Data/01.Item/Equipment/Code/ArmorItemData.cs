/// <summary>
/// 방패 및 방어구 데이터 SO (명세서 5절 준수)
/// </summary>
[UnityEngine.CreateAssetMenu(fileName = "NewArmorItemData", menuName = "Inventory/ItemData/ArmorItemData")]
public class ArmorItemData : EquipmentItemData
{
    [UnityEngine.Header("방어구 방어력 및 경감률")]
    [UnityEngine.SerializeField] private float _flatDefense = 20f;
    [UnityEngine.Range(0f, 0.5f)]
    [UnityEngine.SerializeField] private float _armorAbsorption = 0.10f;
    [UnityEngine.SerializeField] private float _poiseBonus = 5.0f;

    public float FlatDefense => _flatDefense;
    public float ArmorAbsorption => _armorAbsorption;
    public float PoiseBonus => _poiseBonus;

    public override Item CreateItem() => new ArmorItem(this);
}