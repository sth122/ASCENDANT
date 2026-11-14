public class QuickSlotView : UIBase
{
    [UnityEngine.Header("Quick Slot Items (0 to 3)")]
    [UnityEngine.SerializeField] private QuickSlotItemUI[] _slotUIs = new QuickSlotItemUI[QuickSlotSystem.MaxQuickSlots];

    private QuickSlotSystem _system;
    private QuickSlotModel _cachedModel;
    private bool _wasCoolingDown = false;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.HUD;
    }

    public void BindSystem(QuickSlotSystem system)
    {
        _system = system;
    }

    public void RefreshSlots(QuickSlotModel model)
    {
        if (model == null) return;
        _cachedModel = model;

        float cdRatio = _system != null ? _system.RemainingCooldownRatio : 0f;
        for (int i = 0; i < _slotUIs.Length; i++)
        {
            if (_slotUIs[i] == null) continue;
            var data = model.Slots[i];
            _slotUIs[i].UpdateSlotUI(data.Icon, data.Amount, data.IsEmpty ? 0f : cdRatio);
        }
    }

    private void Update()
    {
        if (_system == null || _cachedModel == null) return;

        bool isCooling = _system.IsOnGlobalCooldown;
        if (isCooling || _wasCoolingDown)
        {
            float ratio = isCooling ? _system.RemainingCooldownRatio : 0f;
            for (int i = 0; i < _slotUIs.Length; i++)
            {
                if (_slotUIs[i] == null) continue;
                var data = _cachedModel.Slots[i];
                _slotUIs[i].UpdateSlotUI(data.Icon, data.Amount, data.IsEmpty ? 0f : ratio);
            }
            _wasCoolingDown = isCooling;
        }
    }
}