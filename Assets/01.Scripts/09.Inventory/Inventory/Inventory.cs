using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 전체 아이템 보관, 슬롯 스왑, 장비 장착/해제를 총괄하는 내부 프로세스 클래스
/// </summary>
public class Inventory
{
    public const int DefaultCapacity = 30;
    private readonly Item[] _slots = new Item[DefaultCapacity];

    // 8대 장비 슬롯 (머리, 상의, 장갑, 신발 / 목걸이, 반지 / 한손검, 방패)
    private readonly Dictionary<EquipSlotType, EquipmentItem> _equipmentSlots = new Dictionary<EquipSlotType, EquipmentItem>();

    public QuickSlotSystem QuickSlots { get; } = new QuickSlotSystem();

    public event Action<int, Item> OnSlotUpdated;
    public event Action<EquipSlotType, EquipmentItem> OnEquipmentChanged;

    public Item GetItem(int index) => (index >= 0 && index < DefaultCapacity) ? _slots[index] : null;
    public EquipmentItem GetEquippedItem(EquipSlotType slot) => _equipmentSlots.TryGetValue(slot, out var item) ? item : null;

    /// <summary>
    /// 인벤토리에 아이템 추가
    /// </summary>
    public bool AddItem(ItemData data, int amount = 1)
    {
        if (data == null || amount <= 0) return false;

        // 1. 수량형 아이템인 경우 기존 스택에 합산
        if (data is CountableItemData countableData)
        {
            for (int i = 0; i < DefaultCapacity; i++)
            {
                if (_slots[i] is CountableItem existing && existing.Data.Id == data.Id)
                {
                    amount = existing.AddAmountAndGetOverflow(amount);
                    OnSlotUpdated?.Invoke(i, existing);
                    if (amount == 0) return true;
                }
            }
        }

        // 2. 빈 슬롯에 새 아이템 인스턴스 생성 배치
        while (amount > 0)
        {
            int emptyIndex = FindEmptySlotIndex();
            if (emptyIndex == -1) return false;

            Item newItem = data.CreateItem();
            if (newItem is CountableItem countable)
            {
                int putAmount = Mathf.Min(amount, countable.CountableData.MaxStack);
                countable.SetAmount(putAmount);
                amount -= putAmount;
            }
            else
            {
                amount--;
            }

            _slots[emptyIndex] = newItem;
            OnSlotUpdated?.Invoke(emptyIndex, newItem);
        }

        return true;
    }

    /// <summary>
    /// 장비 장착 처리 (인벤토리 슬롯 <-> 장착 슬롯 스왑)
    /// </summary>
    public bool EquipItem(int inventorySlotIndex, PlayerController player)
    {
        if (inventorySlotIndex < 0 || inventorySlotIndex >= DefaultCapacity) return false;
        if (!(_slots[inventorySlotIndex] is EquipmentItem equipItem)) return false;

        PlayerInfo info = player.StatusController.PlayerInfoSource;
        if (info.Strength < equipItem.EquipData.RequiredStrength || info.Dexterity < equipItem.EquipData.RequiredDexterity)
        {
            DebugLogger.LogWarningWithTag("Inventory", "요구 스탯(STR/DEX) 미달로 장착할 수 없습니다.");
            return false;
        }

        EquipSlotType targetSlot = equipItem.EquipData.EquipSlot;
        _equipmentSlots.TryGetValue(targetSlot, out EquipmentItem previousEquip);

        // 장착 슬롯 등록
        _equipmentSlots[targetSlot] = equipItem;
        _slots[inventorySlotIndex] = previousEquip; // 기존 장착품은 가방으로

        OnEquipmentChanged?.Invoke(targetSlot, equipItem);
        OnSlotUpdated?.Invoke(inventorySlotIndex, previousEquip);

        RecalculateEquipStats(player);
        return true;
    }

    /// <summary>
    /// 장비 해제
    /// </summary>
    public bool UnequipItem(EquipSlotType slot, PlayerController player)
    {
        if (!_equipmentSlots.TryGetValue(slot, out EquipmentItem equipped) || equipped == null) return false;

        int emptyIndex = FindEmptySlotIndex();
        if (emptyIndex == -1) return false;

        _equipmentSlots[slot] = null;
        _slots[emptyIndex] = equipped;

        OnEquipmentChanged?.Invoke(slot, null);
        OnSlotUpdated?.Invoke(emptyIndex, equipped);

        // 메인 무기 해제 시 CombatModule의 무기 데이터를 반드시 null로 소거 (유령 무기 버그 차단)
        if (slot == EquipSlotType.MainWeapon)
        {
            player.CombatModule.SetWeaponData(null);
        }

        RecalculateEquipStats(player);
        return true;
    }

    /// <summary>
    /// 장비 변경 후 플레이어의 중량비 및 전투 모듈 즉각 갱신
    /// </summary>
    public void RecalculateEquipStats(PlayerController player)
    {
        EquipmentTotalStats stats = new EquipmentTotalStats();

        foreach (var pair in _equipmentSlots)
        {
            EquipmentItem equip = pair.Value;
            if (equip == null) continue;

            stats.TotalWeight += equip.Data.Weight;

            if (pair.Key == EquipSlotType.MainWeapon && equip is WeaponItem weapon)
            {
                player.CombatModule.SetWeaponData(weapon.WeaponData);
            }
            else if (pair.Key == EquipSlotType.Shield && equip is ShieldItem shield)
            {
                stats.ShieldStability = shield.ShieldData.Stability;
                stats.ShieldAbsorption = shield.ShieldData.PhysicalAbsorption;
            }
            else if (equip is ArmorItem armor)
            {
                stats.TotalFlatDefense += armor.ArmorData.FlatDefense;
                stats.TotalAbsorption += armor.ArmorData.ArmorAbsorption;
                stats.TotalPoiseBonus += armor.ArmorData.PoiseBonus;
            }
            else if (equip is AccessoryItem accessory)
            {
                stats.BonusStrength += accessory.AccessoryData.BonusStrength;
                stats.BonusDexterity += accessory.AccessoryData.BonusDexterity;
                stats.BonusMaxHp += accessory.AccessoryData.BonusMaxHp;
                stats.BonusMaxStamina += accessory.AccessoryData.BonusMaxStamina;
                stats.BonusAttackPower += accessory.AccessoryData.BonusAttackPower;
                stats.TotalFlatDefense += accessory.AccessoryData.BonusFlatDefense;
            }
        }

        if (!_equipmentSlots.ContainsKey(EquipSlotType.MainWeapon) || _equipmentSlots[EquipSlotType.MainWeapon] == null)
        {
            player.CombatModule.SetWeaponData(null);
        }

        player.StatusController.ApplyEquipmentStats(in stats);
    }

    /// <summary>
    /// 수량이 0이 된 소모품 인스턴스를 인벤토리 슬롯 배열에서 완전 소거
    /// </summary>
    public void CleanDepletedSlot(Item itemInstance)
    {
        if (itemInstance == null) return;
        for (int i = 0; i < DefaultCapacity; i++)
        {
            if (ReferenceEquals(_slots[i], itemInstance))
            {
                _slots[i] = null;
                OnSlotUpdated?.Invoke(i, null);
                break;
            }
        }
    }

    private int FindEmptySlotIndex()
    {
        for (int i = 0; i < DefaultCapacity; i++)
        {
            if (_slots[i] == null) return i;
        }
        return -1;
    }

    #region MyRegion
    /// <summary>
    /// 게임 시작 시 인벤토리 슬롯 경유 없이 장착 슬롯에 장비를 직접 장착하고 스탯을 재연산
    /// </summary>
    /// <param name="equipData">장착할 장비 SO 데이터</param>
    /// <param name="player">수치를 적용받을 플레이어 본체</param>
    public bool EquipDirect(EquipmentItemData equipData, PlayerController player)
    {
        if (equipData == null || player == null) return false;

        EquipmentItem newItem = (EquipmentItem)equipData.CreateItem();
        EquipSlotType slotType = equipData.EquipSlot;

        _equipmentSlots[slotType] = newItem;
        OnEquipmentChanged?.Invoke(slotType, newItem);

        RecalculateEquipStats(player);
        return true;
    }

    /// <summary>
    /// PlayerInitialEquipmentSO 데이터를 파싱하여 초기 장비, 소모품 적재 및 퀵슬롯 등록을 일괄 수행
    /// </summary>
    public void ApplyInitialEquipmentLoadout(PlayerInitialEquipmentSO loadout, PlayerController player)
    {
        if (loadout == null || player == null) return;

        // 1. 초기 무기 및 방패 장착
        if (loadout.StartingWeapon != null) EquipDirect(loadout.StartingWeapon, player);
        if (loadout.StartingShield != null) EquipDirect(loadout.StartingShield, player);

        // 2. 4부위 방어구 장착
        if (loadout.StartingHelmet != null) EquipDirect(loadout.StartingHelmet, player);
        if (loadout.StartingChestArmor != null) EquipDirect(loadout.StartingChestArmor, player);
        if (loadout.StartingGauntlets != null) EquipDirect(loadout.StartingGauntlets, player);
        if (loadout.StartingBoots != null) EquipDirect(loadout.StartingBoots, player);

        if(loadout.StartingNecklace != null) EquipDirect(loadout.StartingNecklace, player);
        if(loadout.StartingRing != null) EquipDirect(loadout.StartingRing, player);

        // 3. 소모품 인벤토리 추가 및 지정된 퀵슬롯 등록
        for (int i = 0; i < loadout.StartingConsumables.Count; i++)
        {
            var entry = loadout.StartingConsumables[i];
            if (entry.ItemData == null || entry.Amount <= 0) continue;

            AddItem(entry.ItemData, entry.Amount);

            if (entry.AutoAssignQuickSlotIndex >= 0 && entry.AutoAssignQuickSlotIndex < QuickSlotSystem.MaxQuickSlots)
            {
                for (int slotIdx = 0; slotIdx < DefaultCapacity; slotIdx++)
                {
                    if (_slots[slotIdx] is PortionItem portion && portion.Data.Id == entry.ItemData.Id)
                    {
                        QuickSlots.RegisterItem(entry.AutoAssignQuickSlotIndex, portion);
                        break;
                    }
                }
            }
        }

        // 4. 기타 가방 보관 아이템 적재
        for (int i = 0; i < loadout.ExtraInventoryItems.Count; i++)
        {
            if (loadout.ExtraInventoryItems[i] != null)
            {
                AddItem(loadout.ExtraInventoryItems[i], 1);
            }
        }

        DebugLogger.LogWithTag("Inventory", "[초기 무기/방패 및 4부위 방어구 로드아웃 지급 완결]");
    }
    #endregion


    #region MyRegion
    /// <summary>
    /// 현재 인벤토리의 가방, 장착 장비, 퀵슬롯 상태를 SaveData 객체로 직렬화
    /// </summary>
    public void ExportToSaveData(PlayerSaveData targetData)
    {
        if (targetData == null) return;

        targetData.InventorySlots.Clear();
        targetData.EquippedSlots.Clear();
        targetData.QuickSlots.Clear();

        // 1. 인벤토리 가방 슬롯
        for (int i = 0; i < DefaultCapacity; i++)
        {
            if (_slots[i] != null && _slots[i].Data != null)
            {
                int amount = (_slots[i] is CountableItem countable) ? countable.CurrentAmount : 1;
                targetData.InventorySlots.Add(new ItemSlotSaveData(i, _slots[i].Data.Id, amount));
            }
        }

        // 2. 착용 장비 슬롯
        foreach (var pair in _equipmentSlots)
        {
            if (pair.Value != null && pair.Value.Data != null)
            {
                targetData.EquippedSlots.Add(new EquipSlotSaveData(pair.Key, pair.Value.Data.Id));
            }
        }

        // 3. 퀵슬롯
        for (int q = 0; q < QuickSlotSystem.MaxQuickSlots; q++)
        {
            var item = QuickSlots.GetItem(q);
            if (item != null && item.Data != null)
            {
                int amount = (item is CountableItem countable) ? countable.CurrentAmount : 1;
                targetData.QuickSlots.Add(new ItemSlotSaveData(q, item.Data.Id, amount));
            }
        }
    }

    /// <summary>
    /// 세션이나 세이브 파일 데이터로부터 인벤토리, 장비, 퀵슬롯을 완전 복원
    /// </summary>
    public void RestoreFromSaveData(PlayerSaveData sourceData, PlayerController player, DataManager dataManager)
    {
        if (sourceData == null || player == null || dataManager == null) return;

        for (int i = 0; i < DefaultCapacity; i++) _slots[i] = null;
        _equipmentSlots.Clear();

        // 1. 가방 슬롯 복원
        for (int i = 0; i < sourceData.InventorySlots.Count; i++)
        {
            var entry = sourceData.InventorySlots[i];
            if (dataManager.TryGetItemDataById(entry.ItemId, out ItemData itemData))
            {
                Item restoredItem = itemData.CreateItem();
                if (restoredItem is CountableItem countable)
                {
                    countable.SetAmount(entry.Amount);
                }
                _slots[entry.SlotIndex] = restoredItem;
                OnSlotUpdated?.Invoke(entry.SlotIndex, restoredItem);
            }
        }

        // 2. 착용 슬롯 복원
        for (int i = 0; i < sourceData.EquippedSlots.Count; i++)
        {
            var entry = sourceData.EquippedSlots[i];
            if (dataManager.TryGetItemDataById(entry.ItemId, out ItemData itemData) && itemData is EquipmentItemData equipData)
            {
                EquipmentItem equipItem = (EquipmentItem)equipData.CreateItem();
                _equipmentSlots[entry.SlotType] = equipItem;
                OnEquipmentChanged?.Invoke(entry.SlotType, equipItem);
            }
        }

        // 3. 퀵슬롯 복원
        for (int q = 0; q < sourceData.QuickSlots.Count; q++)
        {
            var entry = sourceData.QuickSlots[q];
            for (int s = 0; s < DefaultCapacity; s++)
            {
                if (_slots[s] is PortionItem portion && portion.Data.Id == entry.ItemId)
                {
                    QuickSlots.RegisterItem(entry.SlotIndex, portion);
                    break;
                }
            }
        }

        RecalculateEquipStats(player);
    }
    #endregion
}