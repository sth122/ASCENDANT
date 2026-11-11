using UnityEngine;

/// <summary>
/// 포션/화염병 인스턴스 - 기능 분리 필요
/// </summary>
public class ConsumableItem : CountableItem
{
    public ConsumableItemData ItemData => (ConsumableItemData)Data;

    public ConsumableItem(ConsumableItemData data, int amount = 1) : base(data, amount) { }

    public bool Use(PlayerController player)
    {
        if (CurrentAmount <= 0 || player == null) return false;

        var effects = ItemData.Effects;
        if (effects == null || effects.Count == 0) return false;

        bool anySuccess = false;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] != null && effects[i].Execute(player.gameObject))
            {
                anySuccess = true;
            }
        }

        if (anySuccess)
        {
            CurrentAmount--;
            return true;
        }

        return false;
    }
}