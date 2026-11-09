public class QuickSlotView : UIBase
{
    [UnityEngine.Header("Quick Slot Items (0 to 3)")]
    [UnityEngine.SerializeField] private QuickSlotItemUI[] _slotUIs = new QuickSlotItemUI[QuickSlotSystem.MaxQuickSlots];

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.HUD;
    }

    public void RefreshSlots(QuickSlotModel model)
    {
        if (model == null) return;

        for (int i = 0; i < _slotUIs.Length; i++)
        {
            if (_slotUIs[i] == null) continue;

            var data = model.Slots[i];
            bool isSelected = (model.SelectedIndex == i);
            _slotUIs[i].UpdateSlotUI(data.Icon, data.Amount, data.CooldownRatio, isSelected);
        }
    }
}