using System.Collections.Generic;

/// <summary>
/// 플레이어의 영속적 스탯 및 진행 상태를 저장하는 직렬화 데이터 클래스
/// </summary>
[System.Serializable]
public class PlayerSaveData
{
    [UnityEngine.Header("Level & Souls")]
    public int SoulLevel = 1;
    public int CurrentSouls = 0;

    [UnityEngine.Header("Attributes")]
    public int Vigor = 10;
    public int Endurance = 10;
    public int Strength = 10;
    public int Dexterity = 10;

    [UnityEngine.Header("Runtime Status")]
    public float CurrentHp = 650f;

    [UnityEngine.Header("Loadout Flags")]
    [UnityEngine.Tooltip("초기 장비 지급 완료 여부 (새 게임 검증용 영속 플래그)")]
    public bool HasReceivedInitialEquipment = false;

    [UnityEngine.Header("Equipped Item IDs")]
    public List<ItemSlotSaveData> InventorySlots = new List<ItemSlotSaveData>();
    public List<EquipSlotSaveData> EquippedSlots = new List<EquipSlotSaveData>();
    public List<ItemSlotSaveData> QuickSlots = new List<ItemSlotSaveData>();

    public PlayerSaveData() { }

    public PlayerSaveData(PlayerInfo status, float currentHp)
    {
        if (status == null) return;

        SoulLevel = status.SoulLevel;
        CurrentSouls = status.CurrentSouls;
        Vigor = status.Vigor;
        Endurance = status.Endurance;
        Strength = status.Strength;
        Dexterity = status.Dexterity;
        CurrentHp = currentHp;
    }
}

[System.Serializable]
public struct ItemSlotSaveData
{
    public int SlotIndex;
    public int ItemId;
    public int Amount;

    public ItemSlotSaveData(int slotIndex, int itemId, int amount)
    {
        SlotIndex = slotIndex;
        ItemId = itemId;
        Amount = amount;
    }
}

[System.Serializable]
public struct EquipSlotSaveData
{
    public EquipSlotType SlotType;
    public int ItemId;

    public EquipSlotSaveData(EquipSlotType slotType, int itemId)
    {
        SlotType = slotType;
        ItemId = itemId;
    }
}