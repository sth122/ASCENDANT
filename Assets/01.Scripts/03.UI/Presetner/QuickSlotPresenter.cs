// QuickSlotPresenter.cs 전체 교체
public class QuickSlotPresenter : UIPresenterBase<QuickSlotView, QuickSlotModel>
{
    private QuickSlotSystem _system;

    protected override void OnBind()
    {
        ResolveQuickSlotSystem();
        OnBindHandler();
        SynchronizeFullModel();

        if (View != null)
        {
            View.BindSystem(_system);
        }
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
        if (View != null)
        {
            View.BindSystem(null);
        }
        _system = null;
    }

    private void ResolveQuickSlotSystem()
    {
        if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned)
        {
            if (provider.Player.TryGetComponent(out PlayerInventoryHandler handler) && handler.InventoryInstance != null)
            {
                _system = handler.InventoryInstance.QuickSlots;
            }
        }
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();
        if (_system != null)
        {
            _system.OnQuickSlotChanged += HandleQuickSlotChanged;
        }
    }

    protected override void OnUnbindHandler()
    {
        if (_system != null)
        {
            _system.OnQuickSlotChanged -= HandleQuickSlotChanged;
        }
    }

    private void HandleQuickSlotChanged(int slotIndex, ConsumableItem item)
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

    private void SynchronizeFullModel()
    {
        if (_system == null || Model == null || View == null) return;

        for (int i = 0; i < QuickSlotSystem.MaxQuickSlots; i++)
        {
            ConsumableItem item = _system.GetItem(i) as ConsumableItem;
            HandleQuickSlotChanged(i, item);
        }
    }
}