#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 다크소울 3 스타일 1920x1080 3-Column 인벤토리 프리팹을 빌드하고
/// Scroll View 하위 30개 그리드 슬롯 및 레퍼런스를 100% 자동 직렬화하여 영구 저장하는 에디터 툴
/// </summary>
public static class DS3InventoryMockupBuilder
{
    private const string PrefabSavePath = "Assets/07.Prefabs/UI/DS3_InventoryView.prefab";

    [MenuItem("ASCENDANT/UI/Build DS3 Inventory Prefab")]
    public static void BuildPrefab()
    {
        // 1. 루트 오브젝트 생성 (1920x1080)
        GameObject rootGo = new GameObject("DS3_InventoryView", typeof(RectTransform), typeof(CanvasGroup), typeof(InventoryView));
        RectTransform rootRect = rootGo.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(1920f, 1080f);
        SetFullStretch(rootRect);

        Image rootBg = rootGo.AddComponent<Image>();
        rootBg.color = new Color(0.04f, 0.04f, 0.04f, 0.94f);

        // 2. 3-Column 컨테이너 생성
        RectTransform leftCol = CreateColumn("Left_Column_Slots", rootGo.transform, new Vector2(0f, 0f), new Vector2(0.35f, 1f), new Vector2(30f, 30f), new Vector2(-15f, -30f));
        RectTransform centerCol = CreateColumn("Center_Column_Details", rootGo.transform, new Vector2(0.35f, 0f), new Vector2(0.70f, 1f), new Vector2(15f, 30f), new Vector2(-15f, -30f));
        RectTransform rightCol = CreateColumn("Right_Column_CharacterData", rootGo.transform, new Vector2(0.70f, 0f), new Vector2(1.0f, 1f), new Vector2(15f, 30f), new Vector2(-30f, -30f));

        // 3. 서브 패널 빌드
        ItemDetailPaneUI detailPane = BuildCenterColumn(centerCol);
        CharacterDataPaneUI charDataPane = BuildRightColumn(rightCol);
        Button closeBtn = BuildCloseButton(rootGo.transform);

        // 4. 좌측 탭, 스크롤 뷰(Content 30슬롯 물리 생성), 6대 장비 슬롯, 4대 퀵슬롯 등록 영역 생성
        var leftLayout = BuildLeftColumn(leftCol);

        // 5. SerializedObject를 통해 모든 레퍼런스를 인스펙터에 영구 직렬화
        InventoryView inventoryView = rootGo.GetComponent<InventoryView>();
        SerializedObject so = new SerializedObject(inventoryView);

        so.FindProperty("_detailPaneUI").objectReferenceValue = detailPane;
        so.FindProperty("_characterDataPaneUI").objectReferenceValue = charDataPane;
        so.FindProperty("_closeButton").objectReferenceValue = closeBtn;

        so.FindProperty("_weaponTabButton").objectReferenceValue = leftLayout.WeaponTab;
        so.FindProperty("_shieldTabButton").objectReferenceValue = leftLayout.ShieldTab;
        so.FindProperty("_armorTabButton").objectReferenceValue = leftLayout.ArmorTab;
        so.FindProperty("_consumableTabButton").objectReferenceValue = leftLayout.ConsumableTab;

        so.FindProperty("_mainWeaponSlot").objectReferenceValue = leftLayout.MainWeapon;
        so.FindProperty("_shieldSlot").objectReferenceValue = leftLayout.Shield;
        so.FindProperty("_helmetSlot").objectReferenceValue = leftLayout.Helmet;
        so.FindProperty("_chestArmorSlot").objectReferenceValue = leftLayout.ChestArmor;
        so.FindProperty("_leggingsSlot").objectReferenceValue = leftLayout.Leggings;
        so.FindProperty("_bootsSlot").objectReferenceValue = leftLayout.Boots;

        // 30개 그리드 슬롯 물리 바인딩 주입
        SerializedProperty gridArrayProp = so.FindProperty("_gridSlots");
        gridArrayProp.arraySize = leftLayout.GridSlots.Length;
        for (int i = 0; i < leftLayout.GridSlots.Length; i++)
        {
            gridArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = leftLayout.GridSlots[i];
        }

        // 4개 퀵슬롯 등록 슬롯 바인딩 주입
        SerializedProperty quickProp = so.FindProperty("_quickAssignSlots");
        quickProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            quickProp.GetArrayElementAtIndex(i).objectReferenceValue = leftLayout.QuickSlots[i];
        }

        so.ApplyModifiedProperties();

        // 6. 물리 프리팹 에셋 저장
        EnsureDirectory("Assets/07.Prefabs/UI");
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabSavePath);
        Object.DestroyImmediate(rootGo);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = savedPrefab;

        Debug.Log($"[DS3 Inventory 프리팹 빌드 완결] 경로: {PrefabSavePath}");
    }

    private struct LeftColumnLayout
    {
        public Button WeaponTab;
        public Button ShieldTab;
        public Button ArmorTab;
        public Button ConsumableTab;
        public ItemSlotUI[] GridSlots;
        public ItemSlotUI MainWeapon;
        public ItemSlotUI Shield;
        public ItemSlotUI Helmet;
        public ItemSlotUI ChestArmor;
        public ItemSlotUI Leggings;
        public ItemSlotUI Boots;
        public ItemSlotUI[] QuickSlots;
    }

    private static LeftColumnLayout BuildLeftColumn(RectTransform parent)
    {
        LeftColumnLayout layout = new LeftColumnLayout();

        // 1. 상단 탭 바 (갈색 영역)
        GameObject tabBar = CreateUIObject("Category_TabBar", parent);
        RectTransform tabRect = tabBar.GetComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0f, 0.94f);
        tabRect.anchorMax = new Vector2(1f, 1f);
        tabRect.offsetMin = Vector2.zero;
        tabRect.offsetMax = Vector2.zero;
        HorizontalLayoutGroup hLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
        hLayout.childControlWidth = true;
        hLayout.childForceExpandWidth = true;
        hLayout.spacing = 6f;

        layout.WeaponTab = CreateTabButton(tabBar.transform, "무기");
        layout.ShieldTab = CreateTabButton(tabBar.transform, "방패");
        layout.ArmorTab = CreateTabButton(tabBar.transform, "방어구");
        layout.ConsumableTab = CreateTabButton(tabBar.transform, "소비아이템");

        // 2. 중앙 좌측 Scroll View 기반 아이템 슬롯 그리드 (흰색 영역: 30개 슬롯 물리 생성)
        GameObject scrollViewGo = CreateUIObject("Item_ScrollView", parent);
        RectTransform scrollRectTransform = scrollViewGo.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0.44f);
        scrollRectTransform.anchorMax = new Vector2(1f, 0.93f);
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;
        ScrollRect scrollRect = scrollViewGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Viewport 생성 및 마스크 적용
        GameObject viewportGo = CreateUIObject("Viewport", scrollViewGo.transform);
        RectTransform viewportRect = viewportGo.GetComponent<RectTransform>();
        SetFullStretch(viewportRect);
        Image viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = new Color(0f, 0f, 0f, 0.2f);
        Mask mask = viewportGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content 생성 및 GridLayoutGroup 적용
        GameObject contentGo = CreateUIObject("Content", viewportGo.transform);
        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 600f);
        GridLayoutGroup grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(85f, 85f);
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        layout.GridSlots = new ItemSlotUI[30];
        for (int i = 0; i < 30; i++)
        {
            layout.GridSlots[i] = CreateSlotUI($"GridSlot_{i:D2}", contentGo.transform);
            layout.GridSlots[i].InitializeSlot(i);
        }

        // 3. 하단 좌측 6대 장비 슬롯 (보라/자주색 영역)
        GameObject equipRoot = CreateUIObject("EquipSlots_Row", parent);
        RectTransform equipRect = equipRoot.GetComponent<RectTransform>();
        equipRect.anchorMin = new Vector2(0f, 0.16f);
        equipRect.anchorMax = new Vector2(1f, 0.42f);
        equipRect.offsetMin = Vector2.zero;
        equipRect.offsetMax = Vector2.zero;
        GridLayoutGroup eGrid = equipRoot.AddComponent<GridLayoutGroup>();
        eGrid.cellSize = new Vector2(85f, 85f);
        eGrid.spacing = new Vector2(8f, 8f);
        eGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        eGrid.constraintCount = 3;

        layout.MainWeapon = CreateSlotUI("Slot_MainWeapon", equipRoot.transform);
        layout.Shield = CreateSlotUI("Slot_Shield", equipRoot.transform);
        layout.Helmet = CreateSlotUI("Slot_Helmet", equipRoot.transform);
        layout.ChestArmor = CreateSlotUI("Slot_ChestArmor", equipRoot.transform);
        layout.Leggings = CreateSlotUI("Slot_Leggings", equipRoot.transform);
        layout.Boots = CreateSlotUI("Slot_Boots", equipRoot.transform);

        // 4. 최하단 좌측 4대 퀵슬롯 등록 바 (파란색 영역)
        GameObject quickRoot = CreateUIObject("QuickAssignSlots_Row", parent);
        RectTransform qRect = quickRoot.GetComponent<RectTransform>();
        qRect.anchorMin = new Vector2(0f, 0f);
        qRect.anchorMax = new Vector2(1f, 0.14f);
        qRect.offsetMin = Vector2.zero;
        qRect.offsetMax = Vector2.zero;
        HorizontalLayoutGroup qLayout = quickRoot.AddComponent<HorizontalLayoutGroup>();
        qLayout.spacing = 10f;
        qLayout.childControlWidth = false;
        qLayout.childControlHeight = false;

        layout.QuickSlots = new ItemSlotUI[4];
        for (int i = 0; i < 4; i++)
        {
            layout.QuickSlots[i] = CreateSlotUI($"Slot_Quick_{i}", quickRoot.transform);
            layout.QuickSlots[i].GetComponent<RectTransform>().sizeDelta = new Vector2(75f, 75f);
            layout.QuickSlots[i].InitializeQuickAssignSlot(i);
        }

        return layout;
    }

    private static Button CreateTabButton(Transform parent, string labelText)
    {
        GameObject btnObj = CreateUIObject($"Tab_{labelText}", parent);
        btnObj.AddComponent<Image>().color = new Color(0.2f, 0.16f, 0.12f, 1f);
        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = CreateUIObject("Text", btnObj.transform);
        SetFullStretch(txtObj.GetComponent<RectTransform>());
        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = labelText;
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    private static ItemDetailPaneUI BuildCenterColumn(RectTransform parent)
    {
        GameObject paneObj = CreateUIObject("DetailPane", parent);
        SetFullStretch(paneObj.GetComponent<RectTransform>());
        return paneObj.AddComponent<ItemDetailPaneUI>();
    }

    private static CharacterDataPaneUI BuildRightColumn(RectTransform parent)
    {
        GameObject charObj = CreateUIObject("CharacterDataPane", parent);
        SetFullStretch(charObj.GetComponent<RectTransform>());
        return charObj.AddComponent<CharacterDataPaneUI>();
    }

    private static Button BuildCloseButton(Transform parent)
    {
        GameObject btnObj = CreateUIObject("Btn_Close", parent);
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(40f, 40f);
        rect.anchoredPosition = new Vector2(-20f, -20f);
        btnObj.AddComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 1f);
        return btnObj.AddComponent<Button>();
    }

    private static ItemSlotUI CreateSlotUI(string name, Transform parent)
    {
        GameObject slotObj = CreateUIObject(name, parent);
        slotObj.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

        GameObject iconObj = CreateUIObject("Icon", slotObj.transform);
        SetFullStretch(iconObj.GetComponent<RectTransform>());
        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.raycastTarget = false;
        iconObj.SetActive(false);

        GameObject amountObj = CreateUIObject("Amount", slotObj.transform);
        RectTransform aRect = amountObj.GetComponent<RectTransform>();
        aRect.anchorMin = new Vector2(1f, 0f);
        aRect.anchorMax = new Vector2(1f, 0f);
        aRect.pivot = new Vector2(1f, 0f);
        aRect.sizeDelta = new Vector2(40f, 25f);
        TextMeshProUGUI amountTmp = amountObj.AddComponent<TextMeshProUGUI>();
        amountTmp.fontSize = 16;
        amountTmp.fontStyle = FontStyles.Bold;
        amountTmp.alignment = TextAlignmentOptions.BottomRight;
        amountObj.SetActive(false);

        ItemSlotUI slotUI = slotObj.AddComponent<ItemSlotUI>();
        SerializedObject so = new SerializedObject(slotUI);
        so.FindProperty("_iconImage").objectReferenceValue = iconImg;
        so.FindProperty("_amountText").objectReferenceValue = amountTmp;
        so.ApplyModifiedProperties();

        return slotUI;
    }

    private static RectTransform CreateColumn(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        GameObject col = CreateUIObject(name, parent);
        RectTransform rect = col.GetComponent<RectTransform>();
        rect.anchorMin = aMin;
        rect.anchorMax = aMax;
        rect.offsetMin = oMin;
        rect.offsetMax = oMax;
        return rect;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureDirectory(string path)
    {
        if (!System.IO.Directory.Exists(path))
        {
            System.IO.Directory.CreateDirectory(path);
        }
    }
}
#endif