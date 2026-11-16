using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerInitialEquipment", menuName = "Combat/PlayerInitialEquipmentSO")]
public class PlayerInitialEquipmentSO : ScriptableObject
{
    [System.Serializable]
    public struct ConsumableLoadout
    {
        public ConsumableItemData ItemData;
        public int Amount;
        [Range(-1, 3)] public int AutoAssignQuickSlotIndex;
    }

    [Header("초기 착용 무기 및 방패 (2종)")]
    [SerializeField] private WeaponItemData _startingWeapon;
    [SerializeField] private ShieldItemData _startingShield;


    [Header("초기 지급 소모품")]
    [SerializeField] private List<ConsumableLoadout> _startingConsumables = new List<ConsumableLoadout>();

    [Header("기타 가방 보관 아이템")]
    [SerializeField] private List<ItemData> _extraInventoryItems = new List<ItemData>();

    public WeaponItemData StartingWeapon => _startingWeapon;
    public ShieldItemData StartingShield => _startingShield;
    public IReadOnlyList<ConsumableLoadout> StartingConsumables => _startingConsumables;
    public IReadOnlyList<ItemData> ExtraInventoryItems => _extraInventoryItems;
}