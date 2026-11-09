public class QuickSlotModel : IUIModel
{
    public struct SlotData
    {
        public UnityEngine.Sprite Icon;
        public int Amount;
        public float CooldownRatio;
        public bool IsEmpty;
    }

    public SlotData[] Slots = new SlotData[QuickSlotSystem.MaxQuickSlots];
    public int SelectedIndex = 0;

    public void Reset()
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            Slots[i] = new SlotData { Icon = null, Amount = 0, CooldownRatio = 0f, IsEmpty = true };
        }
        SelectedIndex = 0;
    }
}