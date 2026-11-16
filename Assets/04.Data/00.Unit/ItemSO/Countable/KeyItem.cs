/// <summary>
/// 인벤토리에 적재되는 열쇠 아이템의 런타임 인스턴스 클래스
/// </summary>
public class KeyItem : Item
{
    public KeyItemData KeyData => Data as KeyItemData;

    public KeyItem(KeyItemData data) : base(data) { }
}