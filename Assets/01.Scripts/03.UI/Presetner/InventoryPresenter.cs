using Cysharp.Threading.Tasks;
using UnityEngine.EventSystems;

public class InventoryPresenter : UIPresenterBase<InventoryView, InventoryModel>
{
    private PlayerController _player;
    private Inventory _inventory;
    private ItemCategory _currentCategory = ItemCategory.Weapon;

    protected override void OnBind()
    {
        ResolvePlayerReferences();
        OnBindHandler();

        if (View != null && View.WeaponTabToggle != null)
        {
            View.WeaponTabToggle.isOn = true;
        }
        FilterGridByCategory(ItemCategory.Weapon);
        SynchronizeEquipAndQuickSlots();
        UpdateFullModel();
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
        _player = null;
        _inventory = null;
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();
        if (View == null) return;

        if (View.CloseButton != null) View.CloseButton.onClick.AddListener(HandleCloseClicked);

        if (View.WeaponTabToggle != null) View.WeaponTabToggle.onValueChanged.AddListener((isOn) => { if (isOn) FilterGridByCategory(ItemCategory.Weapon); });
        if (View.ShieldTabToggle != null) View.ShieldTabToggle.onValueChanged.AddListener((isOn) => { if (isOn) FilterGridByCategory(ItemCategory.Shield); });
        if (View.ArmorTabToggle != null) View.ArmorTabToggle.onValueChanged.AddListener((isOn) => { if (isOn) FilterGridByCategory(ItemCategory.Armor); });
        if (View.AccessoryTabToggle != null) View.AccessoryTabToggle.onValueChanged.AddListener((isOn) => { if (isOn) FilterGridByCategory(ItemCategory.Accessory); });
        if (View.ConsumableTabToggle != null) View.ConsumableTabToggle.onValueChanged.AddListener((isOn) => { if (isOn) FilterGridByCategory(ItemCategory.Consumable); });

        for (int i = 0; i < View.GridSlots.Count; i++)
        {
            View.GridSlots[i].OnSlotClickedEvent += HandleSlotClicked;
            View.GridSlots[i].OnSlotHoverEvent += HandleSlotHover;
        }

        BindEquipSlot(View.MainWeaponSlot);
        BindEquipSlot(View.ShieldSlot);
        BindEquipSlot(View.HelmetSlot);
        BindEquipSlot(View.ChestArmorSlot);
        BindEquipSlot(View.GauntletsSlot);
        BindEquipSlot(View.BootsSlot);
        BindEquipSlot(View.NecklaceSlot);
        BindEquipSlot(View.RingSlot);

        for (int i = 0; i < View.QuickAssignSlots.Count; i++)
        {
            if (View.QuickAssignSlots[i] != null) View.QuickAssignSlots[i].OnSlotHoverEvent += HandleSlotHover;
        }

        if (_inventory != null)
        {
            _inventory.OnSlotUpdated += HandleDomainSlotUpdated;
            _inventory.OnEquipmentChanged += HandleDomainEquipmentChanged;
        }
    }

    protected override void OnUnbindHandler()
    {
        if (View == null) return;

        if (View.CloseButton != null) View.CloseButton.onClick.RemoveAllListeners();

        if (View.WeaponTabToggle != null) View.WeaponTabToggle.onValueChanged.RemoveAllListeners();
        if (View.ShieldTabToggle != null) View.ShieldTabToggle.onValueChanged.RemoveAllListeners();
        if (View.ArmorTabToggle != null) View.ArmorTabToggle.onValueChanged.RemoveAllListeners();
        if (View.AccessoryTabToggle != null) View.AccessoryTabToggle.onValueChanged.RemoveAllListeners();
        if (View.ConsumableTabToggle != null) View.ConsumableTabToggle.onValueChanged.RemoveAllListeners();

        for (int i = 0; i < View.GridSlots.Count; i++)
        {
            View.GridSlots[i].OnSlotClickedEvent -= HandleSlotClicked;
            View.GridSlots[i].OnSlotHoverEvent -= HandleSlotHover;
        }

        UnbindEquipSlot(View.MainWeaponSlot);
        UnbindEquipSlot(View.ShieldSlot);
        UnbindEquipSlot(View.HelmetSlot);
        UnbindEquipSlot(View.ChestArmorSlot);
        UnbindEquipSlot(View.GauntletsSlot);
        UnbindEquipSlot(View.BootsSlot);
        UnbindEquipSlot(View.NecklaceSlot);
        UnbindEquipSlot(View.RingSlot);

        if (_inventory != null)
        {
            _inventory.OnSlotUpdated -= HandleDomainSlotUpdated;
            _inventory.OnEquipmentChanged -= HandleDomainEquipmentChanged;
        }
    }

    private void BindEquipSlot(ItemSlotUI slot)
    {
        if (slot == null) return;
        slot.OnSlotClickedEvent += HandleEquipSlotClicked;
        slot.OnSlotHoverEvent += HandleSlotHover;
    }

    private void UnbindEquipSlot(ItemSlotUI slot)
    {
        if (slot == null) return;
        slot.OnSlotClickedEvent -= HandleEquipSlotClicked;
        slot.OnSlotHoverEvent -= HandleSlotHover;
    }

    private void ResolvePlayerReferences()
    {
        if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned)
        {
            _player = provider.Player;
            if (_player != null && _player.TryGetComponent(out PlayerInventoryHandler handler))
            {
                _inventory = handler.InventoryInstance;
            }
        }
    }

    private void FilterGridByCategory(ItemCategory category)
    {
        _currentCategory = category;
        if (_inventory == null || View == null) return;

        int gridIdx = 0;
        for (int i = 0; i < Inventory.DefaultCapacity; i++)
        {
            Item item = _inventory.GetItem(i);
            if (item != null && item.Data != null && item.Data.Category == category)
            {
                if (gridIdx < View.GridSlots.Count)
                {
                    View.GridSlots[gridIdx].BindItem(item);
                    gridIdx++;
                }
            }
        }

        while (gridIdx < View.GridSlots.Count)
        {
            View.GridSlots[gridIdx].ClearSlot();
            gridIdx++;
        }
    }

    private void SynchronizeEquipAndQuickSlots()
    {
        if (_inventory == null || View == null) return;

        if (View.MainWeaponSlot != null) View.MainWeaponSlot.BindItem(_inventory.GetEquippedItem(EquipSlotType.MainWeapon));
        if (View.ShieldSlot != null) View.ShieldSlot.BindItem(_inventory.GetEquippedItem(EquipSlotType.Shield));
        if (View.HelmetSlot != null) View.HelmetSlot.BindItem(_inventory.GetEquippedItem(EquipSlotType.Helmet));
        if (View.ChestArmorSlot != null) View.ChestArmorSlot.BindItem(_inventory.GetEquippedItem(EquipSlotType.ChestArmor));
        if (View.GauntletsSlot != null) View.GauntletsSlot.BindItem(_inventory.GetEquippedItem(EquipSlotType.Gauntlets));
        if (View.BootsSlot != null) View.BootsSlot.BindItem(_inventory.GetEquippedItem(EquipSlotType.Boots));
        if (View.NecklaceSlot != null) View.NecklaceSlot.BindItem(_inventory.GetEquippedItem(EquipSlotType.Necklace));
        if (View.RingSlot != null) View.RingSlot.BindItem(_inventory.GetEquippedItem(EquipSlotType.Ring));

        for (int i = 0; i < View.QuickAssignSlots.Count; i++)
        {
            if (View.QuickAssignSlots[i] != null) View.QuickAssignSlots[i].BindItem(_inventory.QuickSlots.GetItem(i));
        }
    }

    private void HandleSlotClicked(ItemSlotUI slot, PointerEventData.InputButton button)
    {
        if (_inventory == null || _player == null || slot.BoundItem == null) return;

        Model.FocusedItem = slot.BoundItem;
        View?.DetailPaneUI?.ShowDetails(slot.BoundItem);

        if (button == PointerEventData.InputButton.Left && slot.BoundItem is EquipmentItem)
        {
            _inventory.EquipItem(slot.SlotIndex, _player);
            UpdateFullModel();
        }
        else if (button == PointerEventData.InputButton.Right && slot.BoundItem is PortionItem portion)
        {
            _inventory.QuickSlots.RegisterItem(0, portion);
        }
    }

    private void HandleEquipSlotClicked(ItemSlotUI slot, PointerEventData.InputButton button)
    {
        if (_inventory == null || _player == null || !slot.EquipmentSlotType.HasValue) return;

        _inventory.UnequipItem(slot.EquipmentSlotType.Value, _player);
        UpdateFullModel();
    }

    private void HandleSlotHover(ItemSlotUI slot, bool isEnter)
    {
        if (View?.DetailPaneUI == null) return;

        if (isEnter && slot.BoundItem != null)
        {
            View.DetailPaneUI.ShowDetails(slot.BoundItem);
        }
        else if (!isEnter && Model.FocusedItem == null)
        {
            View.DetailPaneUI.ClearDetails();
        }
    }

    private void HandleDomainSlotUpdated(int slotIndex, Item item) => FilterGridByCategory(_currentCategory);
    private void HandleDomainEquipmentChanged(EquipSlotType slotType, EquipmentItem item)
    {
        SynchronizeEquipAndQuickSlots();
        UpdateFullModel();
    }

    private void UpdateFullModel()
    {
        if (_player == null || View == null || Model == null) return;

        PlayerStatusController status = _player.StatusController;
        PlayerInfo info = status.PlayerInfoSource;
        PlayerMotionStatusModule motion = status.PlayerMotionStats;

        Model.SoulLevel = info != null ? info.SoulLevel : 1;
        Model.CurrentSouls = info != null ? info.CurrentSouls : 0;
        Model.Vigor = info != null ? info.Vigor : 10;
        Model.Endurance = info != null ? info.Endurance : 10;
        Model.Strength = info != null ? info.Strength : 10;
        Model.Dexterity = info != null ? info.Dexterity : 10;

        Model.BonusStrength = status.EquippedBonusStrength;
        Model.BonusDexterity = status.EquippedBonusDexterity;

        Model.CurrentHp = status.CurrentHp;
        Model.MaxHp = status.MaxHp;
        Model.CurrentStamina = status.CurrentStamina;
        Model.MaxStamina = status.MaxStamina;

        Model.CurrentWeight = motion != null ? motion.CurrentEquipWeight : 0f;
        Model.MaxWeight = motion != null ? motion.MaxEquipWeight : 40f;
        Model.CurrentRollTier = motion != null ? motion.CurrentRollTier : RollTier.Mid;

        Model.CurrentAR = _player.CombatModule != null ? _player.CombatModule.CalculateCurrentAR() : 0f;
        Model.CurrentPoise = status.CurrentPoise;
        Model.FlatDefense = (info != null ? info.FlatDefense : 10f) + status.EquippedArmorFlatDefense;
        Model.Absorption = status.EquippedArmorAbsorption;

        // 상단 헤더 소지 소울 텍스트 갱신
        if (View.CurrentSoulsText != null)
        {
            View.CurrentSoulsText.text = Model.CurrentSouls.ToString("N0");
        }

        View.CharacterDataPaneUI?.UpdateCharacterData(Model);
    }

    private void HandleCloseClicked() => View?.CloseWindowAsync().Forget();
}