using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryView : UIWindowView
{
    [Header("Top Header Bar")]
    [SerializeField] private TextMeshProUGUI _currentSoulsText;
    [SerializeField] private Button _closeButton;

    [Header("Scroll View Grid (좌측 30칸)")]
    [SerializeField] private List<ItemSlotUI> _gridSlots = new List<ItemSlotUI>(Inventory.DefaultCapacity);

    [Header("Equipment Slots (8대 장비 슬롯)")]
    [SerializeField] private ItemSlotUI _mainWeaponSlot;
    [SerializeField] private ItemSlotUI _shieldSlot;
    [SerializeField] private ItemSlotUI _helmetSlot;
    [SerializeField] private ItemSlotUI _chestArmorSlot;
    [SerializeField] private ItemSlotUI _gauntletsSlot;
    [SerializeField] private ItemSlotUI _bootsSlot;
    [SerializeField] private ItemSlotUI _necklaceSlot;
    [SerializeField] private ItemSlotUI _ringSlot;

    [Header("Quick Slot Bar (최하단 4슬롯)")]
    [SerializeField] private ItemSlotUI[] _quickAssignSlots = new ItemSlotUI[4];

    [Header("Category Tabs (상단 좌측 5대 토글)")]
    [SerializeField] private Toggle _weaponTabToggle;
    [SerializeField] private Toggle _shieldTabToggle;
    [SerializeField] private Toggle _armorTabToggle;
    [SerializeField] private Toggle _accessoryTabToggle;
    [SerializeField] private Toggle _consumableTabToggle;
    [SerializeField] private ToggleGroup _categoryToggleGroup;

    [Header("DS3 Panels")]
    [SerializeField] private ItemDetailPaneUI _detailPaneUI;
    [SerializeField] private CharacterDataPaneUI _characterDataPaneUI;

    public TextMeshProUGUI CurrentSoulsText => _currentSoulsText;
    public Button CloseButton => _closeButton;

    public IReadOnlyList<ItemSlotUI> GridSlots => _gridSlots;
    public ItemSlotUI MainWeaponSlot => _mainWeaponSlot;
    public ItemSlotUI ShieldSlot => _shieldSlot;
    public ItemSlotUI HelmetSlot => _helmetSlot;
    public ItemSlotUI ChestArmorSlot => _chestArmorSlot;
    public ItemSlotUI GauntletsSlot => _gauntletsSlot;
    public ItemSlotUI BootsSlot => _bootsSlot;
    public ItemSlotUI NecklaceSlot => _necklaceSlot;
    public ItemSlotUI RingSlot => _ringSlot;
    public IReadOnlyList<ItemSlotUI> QuickAssignSlots => _quickAssignSlots;

    public Toggle WeaponTabToggle => _weaponTabToggle;
    public Toggle ShieldTabToggle => _shieldTabToggle;
    public Toggle ArmorTabToggle => _armorTabToggle;
    public Toggle AccessoryTabToggle => _accessoryTabToggle;
    public Toggle ConsumableTabToggle => _consumableTabToggle;
    public ItemDetailPaneUI DetailPaneUI => _detailPaneUI;
    public CharacterDataPaneUI CharacterDataPaneUI => _characterDataPaneUI;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.Window;
        _pausePolicy = WindowPausePolicy.PauseTime;

        for (int i = 0; i < _gridSlots.Count; i++)
        {
            if (_gridSlots[i] != null) _gridSlots[i].InitializeSlot(i);
        }

        if (_mainWeaponSlot != null) _mainWeaponSlot.InitializeEquipSlot(EquipSlotType.MainWeapon);
        if (_shieldSlot != null) _shieldSlot.InitializeEquipSlot(EquipSlotType.Shield);
        if (_helmetSlot != null) _helmetSlot.InitializeEquipSlot(EquipSlotType.Helmet);
        if (_chestArmorSlot != null) _chestArmorSlot.InitializeEquipSlot(EquipSlotType.ChestArmor);
        if (_gauntletsSlot != null) _gauntletsSlot.InitializeEquipSlot(EquipSlotType.Gauntlets);
        if (_bootsSlot != null) _bootsSlot.InitializeEquipSlot(EquipSlotType.Boots);
        if (_necklaceSlot != null) _necklaceSlot.InitializeEquipSlot(EquipSlotType.Necklace);
        if (_ringSlot != null) _ringSlot.InitializeEquipSlot(EquipSlotType.Ring);

        for (int i = 0; i < _quickAssignSlots.Length; i++)
        {
            if (_quickAssignSlots[i] != null) _quickAssignSlots[i].InitializeQuickAssignSlot(i);
        }
    }

    public override async UniTask ShowAsync(float duration = 0.15f, System.Threading.CancellationToken ct = default)
    {
        InputSystemHandler.SetMode(InputMode.PausedUI); // TimeScale = 0f 및 마우스 커서 표시
        await base.ShowAsync(duration, ct);
    }

    public override async UniTask CloseWindowAsync(float duration = 0.15f)
    {
        await base.CloseWindowAsync();
        InputSystemHandler.SetMode(InputMode.Gameplay); // TimeScale = 1 및 인게임 커서 잠금 복구
    }
}