public class QuickSlotPresenter : UIPresenterBase<QuickSlotView, QuickSlotModel>
{
    private QuickSlotSystem _system;

    protected override void OnBind()
    {
        ResolveQuickSlotSystem();
        OnBindHandler();
        SynchronizeFullModel();
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
        _system = null;
    }

    private void ResolveQuickSlotSystem()
    {
        if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned)
        {
            if (provider.Player.TryGetComponent(out PlayerInventoryHandler handler))
            {
                _system = handler.InventoryInstance.QuickSlots;
            }
        }
    }

    protected override void OnBindHandler()
    {
        if (_system != null)
        {
            _system.OnQuickSlotChanged += HandleQuickSlotChanged;
            _system.OnSelectedSlotChanged += HandleSelectedSlotChanged;
        }
    }

    protected override void OnUnbindHandler()
    {
        if (_system != null)
        {
            _system.OnQuickSlotChanged -= HandleQuickSlotChanged;
            _system.OnSelectedSlotChanged -= HandleSelectedSlotChanged;
        }
    }

    private void HandleQuickSlotChanged(int slotIndex, PortionItem item)
    {
        if (Model == null || View == null || slotIndex < 0 || slotIndex >= QuickSlotSystem.MaxQuickSlots) return;

        if (item != null && item.CurrentAmount > 0)
        {
            Model.Slots[slotIndex].Icon = item.Data.Icon;
            Model.Slots[slotIndex].Amount = item.CurrentAmount;
            Model.Slots[slotIndex].IsEmpty = false;
        }
        else
        {
            Model.Slots[slotIndex].Icon = null;
            Model.Slots[slotIndex].Amount = 0;
            Model.Slots[slotIndex].IsEmpty = true;
        }

        View.RefreshSlots(Model);
    }

    private void HandleSelectedSlotChanged(int selectedIndex)
    {
        if (Model == null || View == null) return;

        Model.SelectedIndex = selectedIndex;
        View.RefreshSlots(Model);
    }

    private void SynchronizeFullModel()
    {
        if (_system == null || Model == null || View == null) return;

        Model.SelectedIndex = _system.SelectedSlotIndex;
        View.RefreshSlots(Model);
    }
}