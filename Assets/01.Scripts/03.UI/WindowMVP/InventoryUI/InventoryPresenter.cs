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

        if (View.WeaponTabToggle != null)
        {
            View.WeaponTabToggle.onValueChanged.AddListener(
                (isOn) => { if (isOn) FilterGridByCategory(ItemCategory.Weapon); });
        }
        if (View.ShieldTabToggle != null)
        {
            View.ShieldTabToggle.onValueChanged.AddListener(
                (isOn) => { if (isOn) FilterGridByCategory(ItemCategory.Shield); });
        }
        if (View.ArmorTabToggle != null)
        {
            View.ArmorTabToggle.onValueChanged.AddListener(
                (isOn) => { if (isOn) FilterGridByCategory(ItemCategory.Armor); });
        }
        if (View.AccessoryTabToggle != null)
        {
            View.AccessoryTabToggle.onValueChanged.AddListener(
                (isOn) => { if (isOn) FilterGridByCategory(ItemCategory.Accessory); });
        }
        if (View.ConsumableTabToggle != null)
        {
            View.ConsumableTabToggle.onValueChanged.AddListener(
                (isOn) => { if (isOn) FilterGridByCategory(ItemCategory.Consumable); });
        }

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
            _inventory.QuickSlots.OnQuickSlotChanged += HandleDomainQuickSlotChanged;
        }

        EventBusSystem.Subscribe<SoulChangedEvent>(HandleSoulChanged);
    }

    protected override void OnUnbindHandler()
    {
        EventBusSystem.Unsubscribe<SoulChangedEvent>(HandleSoulChanged);

        if (View == null) return;

        if (View.CloseButton != null) View.CloseButton.onClick.RemoveAllListeners();

        if (View.WeaponTabToggle != null)
        {
            View.WeaponTabToggle.onValueChanged.RemoveAllListeners();
        }
        if (View.ShieldTabToggle != null)
        {
            View.ShieldTabToggle.onValueChanged.RemoveAllListeners();
        }
        if (View.ArmorTabToggle != null)
        {
            View.ArmorTabToggle.onValueChanged.RemoveAllListeners();
        }
        if (View.AccessoryTabToggle != null)
        {
            View.AccessoryTabToggle.onValueChanged.RemoveAllListeners();
        }
        if (View.ConsumableTabToggle != null)
        {
            View.ConsumableTabToggle.onValueChanged.RemoveAllListeners();
        }

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

        for (int i = 0; i < View.QuickAssignSlots.Count; i++)
        {
            if (View.QuickAssignSlots[i] != null)
            {
                View.QuickAssignSlots[i].OnSlotClickedEvent -= HandleQuickAssignSlotClicked;
                View.QuickAssignSlots[i].OnSlotHoverEvent -= HandleSlotHover;
            }
        }

        if (_inventory != null)
        {
            _inventory.OnSlotUpdated -= HandleDomainSlotUpdated;
            _inventory.OnEquipmentChanged -= HandleDomainEquipmentChanged;
            _inventory.QuickSlots.OnQuickSlotChanged -= HandleDomainQuickSlotChanged;
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
                    View.GridSlots[gridIdx].BindItem(item, isEquipped: false, inventorySlotIndex: i);
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

        BindEquipSlotSafe(View.MainWeaponSlot, EquipSlotType.MainWeapon);
        BindEquipSlotSafe(View.ShieldSlot, EquipSlotType.Shield);
        BindEquipSlotSafe(View.HelmetSlot, EquipSlotType.Helmet);
        BindEquipSlotSafe(View.ChestArmorSlot, EquipSlotType.ChestArmor);
        BindEquipSlotSafe(View.GauntletsSlot, EquipSlotType.Gauntlets);
        BindEquipSlotSafe(View.BootsSlot, EquipSlotType.Boots);
        BindEquipSlotSafe(View.NecklaceSlot, EquipSlotType.Necklace);
        BindEquipSlotSafe(View.RingSlot, EquipSlotType.Ring);

        for (int i = 0; i < View.QuickAssignSlots.Count; i++)
        {
            if (View.QuickAssignSlots[i] != null)
            {
                View.QuickAssignSlots[i].BindItem(_inventory.QuickSlots.GetItem(i));
            }
        }
    }

    private void BindEquipSlotSafe(ItemSlotUI slotUI, EquipSlotType slotType)
    {
        if (slotUI == null || _inventory == null) return;
        slotUI.BindItem(_inventory.GetEquippedItem(slotType));
    }

    private void ClearSelectionAndDetails()
    {
        if (Model != null)
        {
            Model.FocusedItem = null;
        }
        View?.DetailPaneUI?.ClearDetails();
    }

    private void HandleSlotClicked(ItemSlotUI slot, PointerEventData.InputButton button)
    {
        if (_inventory == null || _player == null || slot == null) return;

        // 빈 슬롯 클릭 시 상세 패널 및 포커스 초기화
        if (slot.BoundItem == null)
        {
            ClearSelectionAndDetails();
            return;
        }

        Item clickedItem = slot.BoundItem;
        Model.FocusedItem = clickedItem;

        // 1. 장비 아이템 좌클릭 -> 실제 Inventory._slots에서 현재 인덱스를 조회해 정확히 장착
        if (button == PointerEventData.InputButton.Left && clickedItem is EquipmentItem)
        {
            int liveSlotIndex = _inventory.FindSlotIndexOf(clickedItem);
            bool equipped = liveSlotIndex >= 0 && _inventory.EquipItem(liveSlotIndex, _player);

            SynchronizeEquipAndQuickSlots();
            FilterGridByCategory(_currentCategory);
            UpdateFullModel();

            View?.DetailPaneUI?.ShowDetails(clickedItem, equipped ? null : _inventory, _player);
            return;
        }

        // 2. 소모품 아이템 클릭 -> 퀵슬롯 자동 등록 또는 토글 해제
        if (clickedItem is ConsumableItem consumable && clickedItem.Data is IQuickSlotUsable)
        {
            int assignedIdx = _inventory.QuickSlots.FindSlotIndex(consumable);

            if (button == PointerEventData.InputButton.Right)
            {
                if (assignedIdx >= 0)
                {
                    _inventory.QuickSlots.UnregisterSlot(assignedIdx);
                }
                else
                {
                    int emptyQuickIdx = _inventory.QuickSlots.FindEmptySlotIndex();
                    if (emptyQuickIdx >= 0)
                    {
                        _inventory.QuickSlots.RegisterItem(emptyQuickIdx, consumable);
                    }
                }
            }
            else if (button == PointerEventData.InputButton.Left && assignedIdx < 0)
            {
                int emptyQuickIdx = _inventory.QuickSlots.FindEmptySlotIndex();
                if (emptyQuickIdx >= 0)
                {
                    _inventory.QuickSlots.RegisterItem(emptyQuickIdx, consumable);
                }
            }
        }

        View?.DetailPaneUI?.ShowDetails(clickedItem, _inventory, _player);
    }

    private void HandleEquipSlotClicked(ItemSlotUI slot, PointerEventData.InputButton button)
    {
        if (_inventory == null || _player == null || slot == null || !slot.EquipmentSlotType.HasValue) return;

        // 빈 장비 슬롯 클릭 시 상세 패널 초기화
        if (slot.BoundItem == null)
        {
            ClearSelectionAndDetails();
            return;
        }

        Item unequippedItem = slot.BoundItem;
        if (_inventory.UnequipItem(slot.EquipmentSlotType.Value, _player))
        {
            SynchronizeEquipAndQuickSlots();
            FilterGridByCategory(_currentCategory);
            UpdateFullModel();

            Model.FocusedItem = unequippedItem;
            View?.DetailPaneUI?.ShowDetails(unequippedItem, _inventory, _player);
        }
        else
        {
            Model.FocusedItem = unequippedItem;
            View?.DetailPaneUI?.ShowDetails(unequippedItem, null, _player);
        }
    }

    private void HandleQuickAssignSlotClicked(ItemSlotUI slot, PointerEventData.InputButton button)
    {
        if (_inventory == null || slot == null || slot.QuickSlotAssignIndex < 0) return;

        // 빈 퀵슬롯 클릭 시 상세 패널 초기화
        if (slot.BoundItem == null)
        {
            ClearSelectionAndDetails();
            return;
        }

        // 퀵슬롯 우클릭 시 등록 해제
        if (button == PointerEventData.InputButton.Right)
        {
            if (Model != null && Model.FocusedItem != null && Model.FocusedItem.InstanceGuid == slot.BoundItem.InstanceGuid)
            {
                ClearSelectionAndDetails();
            }
            _inventory.QuickSlots.UnregisterSlot(slot.QuickSlotAssignIndex);
            return;
        }

        // 좌클릭 시 상세 정보 표시
        Model.FocusedItem = slot.BoundItem;
        View?.DetailPaneUI?.ShowDetails(slot.BoundItem, _inventory, _player);
    }

    private void HandleSlotHover(ItemSlotUI slot, bool isEnter)
    {
        if (View?.DetailPaneUI == null) return;

        if (isEnter && slot.BoundItem != null)
        {
            Inventory compareInv = slot.EquipmentSlotType.HasValue ? null : _inventory;
            View.DetailPaneUI.ShowDetails(slot.BoundItem, compareInv, _player);
        }
        else if (!isEnter)
        {
            if (Model.FocusedItem != null)
            {
                View.DetailPaneUI.ShowDetails(Model.FocusedItem, _inventory, _player);
            }
            else
            {
                View.DetailPaneUI.ClearDetails();
            }
        }
    }
    private void HandleDomainSlotUpdated(int slotIndex, Item item) => FilterGridByCategory(_currentCategory);

    private void HandleDomainEquipmentChanged(EquipSlotType slotType, EquipmentItem item)
    {
        SynchronizeEquipAndQuickSlots();
        UpdateFullModel();
    }

    private void HandleDomainQuickSlotChanged(int slotIndex, ConsumableItem item)
    {
        if (View != null && slotIndex >= 0 && slotIndex < View.QuickAssignSlots.Count && View.QuickAssignSlots[slotIndex] != null)
        {
            View.QuickAssignSlots[slotIndex].BindItem(item);
        }
    }

    private void HandleSoulChanged(SoulChangedEvent evt)
    {
        if (View == null || Model == null) return;

        Model.CurrentSouls = evt.CurrentSouls;
        RefreshSoulsDisplay(evt.CurrentSouls);
        View.CharacterDataPaneUI?.UpdateCharacterData(Model);
    }

    private void RefreshSoulsDisplay(int currentSouls)
    {
        if (View != null && View.CurrentSoulsText != null)
        {
            View.CurrentSoulsText.text = currentSouls.ToString("N0");
        }
    }

    private void UpdateFullModel()
    {
        if (_player == null)
        {
            ResolvePlayerReferences();
        }
        if (_player == null || View == null || Model == null) return;

        PlayerStatusController status = _player.StatusController;
        PlayerInfo info = status.PlayerInfoSource;
        PlayerMotionStatusModule motion = status.PlayerMotionStats;

        Model.SoulLevel = info != null ? info.SoulLevel : 1;
        Model.CurrentSouls = status.CurrentSouls;
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

        RefreshSoulsDisplay(Model.CurrentSouls);
        View.CharacterDataPaneUI?.UpdateCharacterData(Model);
    }

    private void HandleCloseClicked() => View?.CloseWindowAsync().Forget();
}