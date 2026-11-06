[UnityEngine.CreateAssetMenu(fileName = "NewShieldItemData", menuName = "Inventory/ItemData/ShieldItemData")]
public class ShieldItemData : EquipmentItemData
{
    [UnityEngine.Header("방패 고유 사양 (Shield Spec)")]
    [UnityEngine.SerializeField] private ShieldClassification _shieldClass = ShieldClassification.Medium;
    [UnityEngine.Range(0f, 1f)]
    [UnityEngine.SerializeField] private float _physicalAbsorption = 1.0f;
    [UnityEngine.Range(10f, 90f)]
    [UnityEngine.SerializeField] private float _stability = 65f;

    [UnityEngine.Header("패링 프레임 윈도우 (60fps 기준)")]
    [UnityEngine.SerializeField] private int _parryStartFrame = 10;
    [UnityEngine.SerializeField] private int _parryActiveFrames = 6;

    public ShieldClassification ShieldClass => _shieldClass;
    public float PhysicalAbsorption => _physicalAbsorption;
    public float Stability => _stability;
    public float ParryWindowDuration => _parryActiveFrames / 60.0f;

    public override Item CreateItem() => new ShieldItem(this);
}

public class ShieldItem : EquipmentItem
{
    public ShieldItemData ShieldData => (ShieldItemData)Data;
    public ShieldItem(ShieldItemData data) : base(data) { }
}