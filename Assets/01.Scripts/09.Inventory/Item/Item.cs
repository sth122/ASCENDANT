/// <summary>
/// 인벤토리 슬롯에 실존하는 런타임 아이템 인스턴스 최상위 클래스
/// </summary>
public abstract class Item
{
    public ItemData Data { get; }
    public Item(ItemData data) => Data = data;
}