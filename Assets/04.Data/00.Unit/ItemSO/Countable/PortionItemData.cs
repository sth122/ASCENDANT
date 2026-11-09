/// <summary>
/// 포션 및 투척형 소모품 데이터 SO
/// </summary>
[UnityEngine.CreateAssetMenu(fileName = "NewPortionItemData", menuName = "Inventory/ItemData/PortionItemData")]
public class PortionItemData : CountableItemData, IQuickSlotUsable
{
    [UnityEngine.Header("소모 효과 설정")]
    [UnityEngine.SerializeField] private System.Collections.Generic.List<ItemEffectSO> _effects 
        = new System.Collections.Generic.List<ItemEffectSO>();
    [UnityEngine.SerializeField] private float _cooldown = 1.5f;

    public System.Collections.Generic.IReadOnlyList<ItemEffectSO> Effects => _effects;
    public float Cooldown => _cooldown;
    public int ItemId => Id;
    public float CooldownDuration => _cooldown;

    public override Item CreateItem() => new PortionItem(this);
}