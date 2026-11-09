/// <summary>
/// 수량 관리가 가능한 아이템 인스턴스
/// </summary>
public abstract class CountableItem : Item
{
    public CountableItemData CountableData => (CountableItemData)Data;
    public int CurrentAmount { get; protected set; }

    public CountableItem(CountableItemData data, int amount = 1) : base(data)
    {
        SetAmount(amount);
    }

    public void SetAmount(int amount)
    {
        CurrentAmount = UnityEngine.Mathf.Clamp(amount, 0, CountableData.MaxStack);
    }

    public int AddAmountAndGetOverflow(int amount)
    {
        int total = CurrentAmount + amount;
        if (total <= CountableData.MaxStack)
        {
            CurrentAmount = total;
            return 0;
        }

        CurrentAmount = CountableData.MaxStack;
        return total - CountableData.MaxStack;
    }

    public bool TryConsume(int count = 1)
    {
        if (CurrentAmount >= count)
        {
            CurrentAmount -= count;
            return true;
        }
        return false;
    }
}