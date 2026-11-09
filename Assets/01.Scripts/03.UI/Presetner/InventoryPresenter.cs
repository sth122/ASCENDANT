using Cysharp.Threading.Tasks;

/// <summary>
/// InventoryView와 Inventory 도메인을 중계하는 프리젠터
/// </summary>
public class InventoryPresenter : UIPresenterBase<InventoryView, InventoryModel>
{
    private PlayerController _player;
    private Inventory _inventory;
    private ItemCategory _currentCategory = ItemCategory.Weapon;

    protected override void OnBind()
    {
        ResolvePlayerReferences();
        OnBindHandler();
        FilterGridByCategory(ItemCategory.Weapon);
        SynchronizeEquipAndQuickSlots();
        UpdateFullView();
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

        if (View.CloseButton != null)
        {
            View.CloseButton.onClick.AddListener(HandleCloseClicked);
        }

        // 카테고리 탭 버튼 리스너 등록
        if (View.WeaponTabButton != null) View.WeaponTabButton.onClick.AddListener(HandleWeaponTabClicked);
        if (View.ShieldTabButton != null) View.ShieldTabButton.onClick.AddListener(HandleShieldTabClicked);
        if (View.ArmorTabButton != null) View.ArmorTabButton.onClick.AddListener(HandleArmorTabClicked);
        if (View.ConsumableTabButton != null) View.ConsumableTabButton.onClick.AddListener(HandleConsumableTabClicked);

        // 그리드 슬롯 호버 및 클릭 등록
        for (int i = 0; i < View.GridSlots.Count; i++)
        {
            View.GridSlots[i].OnSlotClickedEvent += HandleSlotClicked;
            View.GridSlots[i].OnSlotHoverEvent += HandleSlotHover;
        }

        // 6대 장비 슬롯 등록
        BindEquipSlot(View.MainWeaponSlot);
        BindEquipSlot(View.ShieldSlot);
        BindEquipSlot(View.HelmetSlot);
        BindEquipSlot(View.ChestArmorSlot);
        BindEquipSlot(View.LeggingsSlot);
        BindEquipSlot(View.BootsSlot);

        // 4대 퀵슬롯 등록 슬롯 등록
        for (int i = 0; i < View.QuickAssignSlots.Count; i++)
        {
            if (View.QuickAssignSlots[i] != null)
            {
                View.QuickAssignSlots[i].OnSlotHoverEvent += HandleSlotHover;
            }
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

        if (View.CloseButton != null)
        {
            View.CloseButton.onClick.RemoveListener(HandleCloseClicked);
        }

        if (View.WeaponTabButton != null) View.WeaponTabButton.onClick.RemoveListener(HandleWeaponTabClicked);
        if (View.ShieldTabButton != null) View.ShieldTabButton.onClick.RemoveListener(HandleShieldTabClicked);
        if (View.ArmorTabButton != null) View.ArmorTabButton.onClick.RemoveListener(HandleArmorTabClicked);
        if (View.ConsumableTabButton != null) View.ConsumableTabButton.onClick.RemoveListener(HandleConsumableTabClicked);

        for (int i = 0; i < View.GridSlots.Count; i++)
        {
            View.GridSlots[i].OnSlotClickedEvent -= HandleSlotClicked;
            View.GridSlots[i].OnSlotHoverEvent -= HandleSlotHover;
        }

        UnbindEquipSlot(View.MainWeaponSlot);
        UnbindEquipSlot(View.ShieldSlot);
        UnbindEquipSlot(View.HelmetSlot);
        UnbindEquipSlot(View.ChestArmorSlot);
        UnbindEquipSlot(View.LeggingsSlot);
        UnbindEquipSlot(View.BootsSlot);

        if (_inventory != null)
        {
            _inventory.OnSlotUpdated -= HandleDomainSlotUpdated;
            _inventory.OnEquipmentChanged -= HandleDomainEquipmentChanged;
        }
    }

    private void HandleWeaponTabClicked() => FilterGridByCategory(ItemCategory.Weapon);
    private void HandleShieldTabClicked() => FilterGridByCategory(ItemCategory.Shield);
    private void HandleArmorTabClicked() => FilterGridByCategory(ItemCategory.Armor);
    private void HandleConsumableTabClicked() => FilterGridByCategory(ItemCategory.Consumable);

    /// <summary>
    /// 상단 탭에 맞춰 가방 아이템을 필터링하여 30칸 그리드에 순차 정렬 렌더링
    /// </summary>
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

        // 남은 슬롯은 빈 슬롯으로 클리어
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
        if (View.LeggingsSlot != null) View.LeggingsSlot.BindItem(_inventory.GetEquippedItem(EquipSlotType.Leggings));
        if (View.BootsSlot != null) View.BootsSlot.BindItem(_inventory.GetEquippedItem(EquipSlotType.Boots));

        // 퀵슬롯 등록 상태 동기화
        for (int i = 0; i < View.QuickAssignSlots.Count; i++)
        {
            if (View.QuickAssignSlots[i] != null)
            {
                View.QuickAssignSlots[i].BindItem(_inventory.QuickSlots.GetItem(i));
            }
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

    private void HandleSlotClicked(ItemSlotUI slot, UnityEngine.EventSystems.PointerEventData.InputButton button)
    {
        if (_inventory == null || _player == null || slot.BoundItem == null) return;

        // 선택 아이템 상세창 갱신
        Model.FocusedItem = slot.BoundItem;
        if (View != null && View.DetailPaneUI != null)
        {
            View.DetailPaneUI.ShowDetails(slot.BoundItem);
        }

        // 좌클릭: 장비 아이템 즉각 장착
        if (button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
        {
            if (slot.BoundItem is EquipmentItem)
            {
                _inventory.EquipItem(slot.SlotIndex, _player);
                UpdateFullView();
            }
        }
        // 우클릭: 소모품 퀵슬롯 등록
        else if (button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
        {
            if (slot.BoundItem is PortionItem portion)
            {
                _inventory.QuickSlots.RegisterItem(0, portion);
            }
        }
    }

    private void HandleEquipSlotClicked(ItemSlotUI slot, UnityEngine.EventSystems.PointerEventData.InputButton button)
    {
        if (_inventory == null || _player == null || !slot.EquipmentSlotType.HasValue) return;

        // 장착 해제 실행
        _inventory.UnequipItem(slot.EquipmentSlotType.Value, _player);
        UpdateFullView();
    }

    private void HandleSlotHover(ItemSlotUI slot, bool isEnter)
    {
        if (View == null || View.DetailPaneUI == null) return;

        if (isEnter && slot.BoundItem != null)
        {
            View.DetailPaneUI.ShowDetails(slot.BoundItem);
        }
        else if (!isEnter)
        {
            View.DetailPaneUI.ClearDetails();
        }
    }

    private void HandleDomainSlotUpdated(int slotIndex, Item item)
    {
        if (View != null && slotIndex >= 0 && slotIndex < View.GridSlots.Count)
        {
            View.GridSlots[slotIndex].BindItem(item);
        }
    }

    private void HandleDomainEquipmentChanged(EquipSlotType slotType, EquipmentItem item)
    {
        if (View == null) return;

        switch (slotType)
        {
            case EquipSlotType.MainWeapon:
                if (View.MainWeaponSlot != null) View.MainWeaponSlot.BindItem(item);
                break;
            case EquipSlotType.Shield:
                if (View.ShieldSlot != null) View.ShieldSlot.BindItem(item);
                break;
            case EquipSlotType.Helmet:
                if (View.HelmetSlot != null) View.HelmetSlot.BindItem(item);
                break;
            case EquipSlotType.ChestArmor:
                if (View.ChestArmorSlot != null) View.ChestArmorSlot.BindItem(item);
                break;
            case EquipSlotType.Leggings:
                if (View.LeggingsSlot != null) View.LeggingsSlot.BindItem(item);
                break;
            case EquipSlotType.Boots:
                if (View.BootsSlot != null) View.BootsSlot.BindItem(item);
                break;
        }

        // 장비 변경 즉시 캐릭터 스탯 패널 재연산 및 UI 반영
        UpdateFullView();
    }

    private void UpdateFullView()
    {
        if (_player == null || View == null) return;

        PlayerStatusController status = _player.StatusController;
        PlayerInfo info = status.PlayerInfoSource;
        PlayerMotionStatusModule motion = status.PlayerMotionStats;

        float curWeight = motion != null ? motion.CurrentEquipWeight : 0f;
        RollTier tier = motion != null ? motion.CurrentRollTier : RollTier.Mid;

        float curHp = status.CurrentHp;
        float curStamina = status is PlayerStatusController pStat ? pStat.CurrentStamina : 100f;
        float curPoise = status.CurrentPoise;

        // 현재 장착된 무기 공격력 산출
        float currentAR = 0f;
        EquipmentItem weapon = _inventory != null ? _inventory.GetEquippedItem(EquipSlotType.MainWeapon) : null;
        if (weapon is WeaponItem wItem && info != null)
        {
            currentAR = wItem.WeaponData.BaseDamage + (info.Strength * wItem.WeaponData.StrScaling) + (info.Dexterity * wItem.WeaponData.DexScaling);
        }

        if (View.CharacterDataPaneUI != null)
        {
            View.CharacterDataPaneUI.UpdateCharacterData(info, curHp, curStamina, curPoise, curWeight, tier, currentAR);
        }
    }

    private void HandleCloseClicked()
    {
        if (View != null)
        {
            View.CloseWindowAsync().Forget();
        }
    }
}