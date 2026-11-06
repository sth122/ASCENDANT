using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerInitialEquipment", menuName = "Combat/PlayerInitialEquipmentSO")]
public class PlayerInitialEquipmentSO : ScriptableObject
{
    [System.Serializable]
    public struct ConsumableLoadout
    {
        [Tooltip("지급할 소모성 아이템 SO")]
        public PortionItemData ItemData;
        [Tooltip("초기 지급 수량")]
        public int Amount;
        [Tooltip("등록할 퀵슬롯 번호 (0~3 / -1이면 퀵슬롯에 등록하지 않고 가방에만 보관)")]
        [Range(-1, 3)]
        public int AutoAssignQuickSlotIndex;
    }

    [Header("초기 착용 장비 (Equipped by Default)")]
    [Tooltip("오른손 기본 무기")]
    [SerializeField] private WeaponItemData _startingWeapon;

    [Tooltip("왼손 기본 방패")]
    [SerializeField] private ShieldItemData _startingShield;

    [Tooltip("머리 투구")]
    [SerializeField] private ArmorItemData _startingHelmet;

    [Tooltip("상체 갑옷")]
    [SerializeField] private ArmorItemData _startingChestArmor;

    [Tooltip("하체 각반")]
    [SerializeField] private ArmorItemData _startingLeggings;

    [Tooltip("발 신발")]
    [SerializeField] private ArmorItemData _startingBoots;

    [Header("초기 지급 소모품 및 퀵슬롯 등록")]
    [SerializeField] private List<ConsumableLoadout> _startingConsumables = new List<ConsumableLoadout>();

    [Header("기타 가방 추가 보관 아이템")]
    [SerializeField] private List<ItemData> _extraInventoryItems = new List<ItemData>();

    #region Properties
    public WeaponItemData StartingWeapon => _startingWeapon;
    public ShieldItemData StartingShield => _startingShield;
    public ArmorItemData StartingHelmet => _startingHelmet;
    public ArmorItemData StartingChestArmor => _startingChestArmor;
    public ArmorItemData StartingLeggings => _startingLeggings;
    public ArmorItemData StartingBoots => _startingBoots;
    public IReadOnlyList<ConsumableLoadout> StartingConsumables => _startingConsumables;
    public IReadOnlyList<ItemData> ExtraInventoryItems => _extraInventoryItems;
    #endregion
}