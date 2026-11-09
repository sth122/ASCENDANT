using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryView : UIWindowView
{
    [Header("Scroll View Grid (중앙 좌측)")]
    [SerializeField] private Transform _slotGridRoot;
    [SerializeField] private List<ItemSlotUI> _gridSlots = new List<ItemSlotUI>(Inventory.DefaultCapacity);

    [Header("Equipment Slots (하단 좌측 6슬롯)")]
    [SerializeField] private ItemSlotUI _mainWeaponSlot;
    [SerializeField] private ItemSlotUI _shieldSlot;
    [SerializeField] private ItemSlotUI _helmetSlot;
    [SerializeField] private ItemSlotUI _chestArmorSlot;
    [SerializeField] private ItemSlotUI _leggingsSlot;
    [SerializeField] private ItemSlotUI _bootsSlot;

    [Header("Quick Slot Assign Bar (최하단 좌측 4슬롯)")]
    [SerializeField] private ItemSlotUI[] _quickAssignSlots = new ItemSlotUI[4];

    [Header("Category Tabs (상단 좌측 4버튼)")]
    [SerializeField] private Button _weaponTabButton;
    [SerializeField] private Button _shieldTabButton;
    [SerializeField] private Button _armorTabButton;
    [SerializeField] private Button _consumableTabButton;

    [Header("DS3 Panels (중앙 상세정보 / 우측 스탯)")]
    [SerializeField] private ItemDetailPaneUI _detailPaneUI;
    [SerializeField] private CharacterDataPaneUI _characterDataPaneUI;
    [SerializeField] private Button _closeButton;

    #region Properties
    public IReadOnlyList<ItemSlotUI> GridSlots => _gridSlots;
    public ItemSlotUI MainWeaponSlot => _mainWeaponSlot;
    public ItemSlotUI ShieldSlot => _shieldSlot;
    public ItemSlotUI HelmetSlot => _helmetSlot;
    public ItemSlotUI ChestArmorSlot => _chestArmorSlot;
    public ItemSlotUI LeggingsSlot => _leggingsSlot;
    public ItemSlotUI BootsSlot => _bootsSlot;
    public IReadOnlyList<ItemSlotUI> QuickAssignSlots => _quickAssignSlots;

    public Button WeaponTabButton => _weaponTabButton;
    public Button ShieldTabButton => _shieldTabButton;
    public Button ArmorTabButton => _armorTabButton;
    public Button ConsumableTabButton => _consumableTabButton;
    public ItemDetailPaneUI DetailPaneUI => _detailPaneUI;
    public CharacterDataPaneUI CharacterDataPaneUI => _characterDataPaneUI;
    public Button CloseButton => _closeButton;
    #endregion

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.Window;
        _pausePolicy = WindowPausePolicy.RealtimeCombat; // 다크소울 실시간 전투 유지

        // 런타임에는 문자열 Find를 일절 호출하지 않고 사전 직렬화된 참조를 즉각 초기화
        InitializeGridSlots();
        InitializeEquipmentSlots();
        InitializeQuickAssignSlots();
    }

    /// <summary>
    /// 사전 직렬화된 30개 그리드 슬롯 인스턴스 초기화 (Zero-Find)
    /// </summary>
    private void InitializeGridSlots()
    {
        if (_gridSlots == null) return;

        for (int i = 0; i < _gridSlots.Count; i++)
        {
            if (_gridSlots[i] != null)
            {
                _gridSlots[i].InitializeSlot(i);
            }
        }
    }

    /// <summary>
    /// 사전 직렬화된 6대 장비 슬롯 타입 부여 (Zero-Find)
    /// </summary>
    private void InitializeEquipmentSlots()
    {
        if (_mainWeaponSlot != null) _mainWeaponSlot.InitializeEquipSlot(EquipSlotType.MainWeapon);
        if (_shieldSlot != null) _shieldSlot.InitializeEquipSlot(EquipSlotType.Shield);
        if (_helmetSlot != null) _helmetSlot.InitializeEquipSlot(EquipSlotType.Helmet);
        if (_chestArmorSlot != null) _chestArmorSlot.InitializeEquipSlot(EquipSlotType.ChestArmor);
        if (_leggingsSlot != null) _leggingsSlot.InitializeEquipSlot(EquipSlotType.Leggings);
        if (_bootsSlot != null) _bootsSlot.InitializeEquipSlot(EquipSlotType.Boots);
    }

    /// <summary>
    /// 사전 직렬화된 4대 퀵슬롯 등록 바 슬롯 인덱스 부여 (Zero-Find)
    /// </summary>
    private void InitializeQuickAssignSlots()
    {
        if (_quickAssignSlots == null) return;

        for (int i = 0; i < _quickAssignSlots.Length; i++)
        {
            if (_quickAssignSlots[i] != null)
            {
                _quickAssignSlots[i].InitializeQuickAssignSlot(i);
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// [에디터 전용] 문자열 Find를 완전히 배제하고, 컴포넌트 타입(Type)과 슬롯 배치 구조를 기반으로
    /// 인스펙터 직렬화 필드를 안전하게 자동 연결하는 헬퍼 메서드
    /// </summary>
    [ContextMenu("Auto Bind Hierarchy Components (Type-Based)")]
    public void AutoBindHierarchyComponents()
    {
        UnityEditor.Undo.RecordObject(this, "Auto Bind Inventory Components (No Find)");

        // 1. 패널 컴포넌트 타입 기반 탐색 (문자열 경로 배제)
        _detailPaneUI = GetComponentInChildren<ItemDetailPaneUI>(true);
        _characterDataPaneUI = GetComponentInChildren<CharacterDataPaneUI>(true);

        // 2. 슬롯 컴포넌트 전수 수집 및 위치/상위 컨테이너 구조 분석 분기
        ItemSlotUI[] allSlots = GetComponentsInChildren<ItemSlotUI>(true);
        _gridSlots.Clear();

        List<ItemSlotUI> equipCandidateList = new List<ItemSlotUI>(6);
        List<ItemSlotUI> quickCandidateList = new List<ItemSlotUI>(4);

        for (int i = 0; i < allSlots.Length; i++)
        {
            ItemSlotUI slot = allSlots[i];
            if (slot == null) continue;

            // 상위 부모 컴포넌트나 레이아웃 그룹의 특성으로 슬롯군 자동 분류
            Transform parentTr = slot.transform.parent;
            if (parentTr == null) continue;

            // 스크롤 뷰의 Viewport/Content 하위에 위치한 경우 -> 가방 그리드 슬롯
            if (parentTr.GetComponentInParent<ScrollRect>() != null)
            {
                _gridSlots.Add(slot);
                slot.InitializeSlot(_gridSlots.Count - 1);
            }
            // 수평 레이아웃을 가진 퀵슬롯 바 하위인 경우
            else if (parentTr.GetComponent<HorizontalLayoutGroup>() != null)
            {
                quickCandidateList.Add(slot);
            }
            // 그리드 레이아웃을 가진 장비 영역 하위인 경우
            else if (parentTr.GetComponent<GridLayoutGroup>() != null)
            {
                equipCandidateList.Add(slot);
            }
        }

        // 3. 6대 장비 슬롯 타입 순차 할당 (하이어라키 형제 순서 기준)
        if (equipCandidateList.Count >= 6)
        {
            _mainWeaponSlot = equipCandidateList[0];
            _shieldSlot = equipCandidateList[1];
            _helmetSlot = equipCandidateList[2];
            _chestArmorSlot = equipCandidateList[3];
            _leggingsSlot = equipCandidateList[4];
            _bootsSlot = equipCandidateList[5];

            InitializeEquipmentSlots();
        }

        // 4. 4대 퀵슬롯 등록 바 순차 할당
        _quickAssignSlots = new ItemSlotUI[4];
        for (int i = 0; i < Mathf.Min(4, quickCandidateList.Count); i++)
        {
            _quickAssignSlots[i] = quickCandidateList[i];
            _quickAssignSlots[i].InitializeQuickAssignSlot(i);
        }

        // 5. 버튼 타입 수집 및 할당 (상단 탭 4개 및 우상단 닫기 버튼)
        Button[] allButtons = GetComponentsInChildren<Button>(true);
        List<Button> tabButtons = new List<Button>(4);

        for (int i = 0; i < allButtons.Length; i++)
        {
            Button btn = allButtons[i];
            if (btn == null) continue;

            // 상위 탭바(HorizontalLayoutGroup) 하위의 탭 버튼들 수집
            if (btn.transform.parent != null && btn.transform.parent.GetComponent<HorizontalLayoutGroup>() != null)
            {
                tabButtons.Add(btn);
            }
            // 앵커가 우상단(1, 1)에 위치한 단독 버튼은 닫기 버튼으로 판별
            else if (btn.GetComponent<RectTransform>().anchorMin == Vector2.one)
            {
                _closeButton = btn;
            }
        }

        if (tabButtons.Count >= 4)
        {
            _weaponTabButton = tabButtons[0];
            _shieldTabButton = tabButtons[1];
            _armorTabButton = tabButtons[2];
            _consumableTabButton = tabButtons[3];
        }

        // 스크롤 그리드 루트 Transform 보관
        ScrollRect scroll = GetComponentInChildren<ScrollRect>(true);
        if (scroll != null && scroll.content != null)
        {
            _slotGridRoot = scroll.content;
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[InventoryView] transform.Find 없는 타입 기반 자동 직렬화 완료!");
    }
#endif
}