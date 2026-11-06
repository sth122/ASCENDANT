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

    // 장착 슬롯 (무기 1, 방패 1, 방어구 1)
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
            if (emptyIndex == -1) return false; // 가방 가득 참

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
        float totalWeight = 0f;
        float totalFlatDefense = 0f;
        float totalAbsorption = 0f;
        float totalPoise = 0f;
        float shieldStability = 0f;
        float shieldAbsorption = 0f;

        foreach (var pair in _equipmentSlots)
        {
            EquipmentItem equip = pair.Value;
            if (equip == null) continue;

            totalWeight += equip.Data.Weight;

            // 1. 무기 교체 반영
            if (pair.Key == EquipSlotType.MainWeapon && equip is WeaponItem weapon)
            {
                player.CombatModule.SetWeaponData(weapon.WeaponData);
            }
            // 2. 방패 사양 반영
            else if (pair.Key == EquipSlotType.Shield && equip is ShieldItem shield)
            {
                shieldStability = shield.ShieldData.Stability;
                shieldAbsorption = shield.ShieldData.PhysicalAbsorption;
            }
            // 3. 4부위 방어구(투구, 갑옷, 각반, 신발) 스탯 합산
            else if (equip is ArmorItem armor)
            {
                totalFlatDefense += armor.ArmorData.FlatDefense;
                totalAbsorption += armor.ArmorData.ArmorAbsorption;
                totalPoise += armor.ArmorData.PoiseBonus;
            }
        }

        // 메인 무기가 해제되어 없을 경우 맨손 상태로 캐시 초기화
        if (!_equipmentSlots.ContainsKey(EquipSlotType.MainWeapon) || _equipmentSlots[EquipSlotType.MainWeapon] == null)
        {
            player.CombatModule.SetWeaponData(null);
        }

        // 스탯 모듈에 방어 사양 및 중량비 일괄 동기화
        player.StatusController.UpdateArmorDefense(totalFlatDefense, totalAbsorption, totalPoise);
        player.StatusController.UpdateShieldSpec(shieldStability, shieldAbsorption);
        player.StatusController.PlayerMotionStats.UpdateWeight(totalWeight);
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
        if (loadout.StartingLeggings != null) EquipDirect(loadout.StartingLeggings, player);
        if (loadout.StartingBoots != null) EquipDirect(loadout.StartingBoots, player);

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
}