public abstract class CountableItemData : ItemData
{
    [UnityEngine.Header("수량 설정 (Stack Settings)")]
    [UnityEngine.SerializeField] private int _maxStack = 99;

    public int MaxStack => _maxStack;
    public override ItemType Type => ItemType.Countable;
}