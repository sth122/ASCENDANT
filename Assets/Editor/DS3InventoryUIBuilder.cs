#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DS3InventoryUIBuilder
{
    private const string SubPrefabPath = "Assets/Prefabs/UI/SubPrefabs";
    private const string WindowPrefabPath = "Assets/Prefabs/UI/Windows";
    private const string HudPrefabPath = "Assets/Prefabs/UI/HUD";

    // 에셋 아틀라스 GUID
    private const string GuidWindowFrame = "f947eb97ff993214f8fc30bff41221f9";  // 메인 외곽 프레임
    private const string GuidPanelBg = "87b7fdc2ff27fa648888ec7b07ed1f3a";  // 가죽 패널 / 헤더 / 체크마크
    private const string GuidSlotAtlas = "fbb6c1d6c8a0a8f45bcf228c2ae1f66e";  // 슬롯 프레임 / 버튼
    private const string GuidIconAtlas = "8b8333c8af9b0c54da038fe9102febb5";  // 8대 장비 슬롯 실루엣 아틀라스

    // 5대 탭 토글 심볼 GUID
    private const string GuidTabWeapon = "68de75fcb914e784482b1bec40594eb4";
    private const string GuidTabArmor = "5236511f86e716d45af9fc52fc6cfa08";
    private const string GuidTabAccessory = "1782f761a728c1b4ca122487fcf43af0";
    private const string GuidTabConsumable = "99f9bafafb30d35429de8b16d61c4abd";

    private static TMP_FontAsset s_font;

    [MenuItem("Tools/Build DS3 UI Prefabs (Precise Layout)", false, 10)]
    public static void BuildAllPrefabs()
    {
        EnsureDirectories();
        s_font = GetPretendardFont();

        GameObject itemSlotPrefab = BuildItemSlotPrefab();
        GameObject equipSlotPrefab = BuildEquipSlotPrefab();
        GameObject quickSlotItemPrefab = BuildQuickSlotItemPrefab();

        BuildInventoryWindowPrefab(itemSlotPrefab, equipSlotPrefab);
        BuildQuickSlotHudPrefab(quickSlotItemPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("UI Builder", "화면 범위 초과가 해결된 다크소울 3 인벤토리 UI 프리팹 생성이 완료되었습니다.", "확인");
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(SubPrefabPath)) Directory.CreateDirectory(SubPrefabPath);
        if (!Directory.Exists(WindowPrefabPath)) Directory.CreateDirectory(WindowPrefabPath);
        if (!Directory.Exists(HudPrefabPath)) Directory.CreateDirectory(HudPrefabPath);
    }

    private static TMP_FontAsset GetPretendardFont()
    {
        string[] guids = AssetDatabase.FindAssets("Pretendard-Regular SDF t:TMP_FontAsset");
        if (guids != null && guids.Length > 0)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
        }
        return null;
    }

    private static Sprite LoadSpriteByFileId(string guid, long fileId, string fallbackName = "")
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;

        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        Sprite fallbackSprite = null;

        for (int i = 0; i < allAssets.Length; i++)
        {
            if (allAssets[i] is Sprite sprite)
            {
                if (fallbackSprite == null) fallbackSprite = sprite;

                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id))
                {
                    if (id == fileId) return sprite;
                }

                if (!string.IsNullOrEmpty(fallbackName) && sprite.name.Contains(fallbackName))
                {
                    return sprite;
                }
            }
        }
        return fallbackSprite;
    }

    #region 1. 서브 프리팹 (ItemSlotUI, EquipSlotUI, QuickSlotItemUI)

    private static GameObject BuildItemSlotPrefab()
    {
        string path = $"{SubPrefabPath}/ItemSlotUI.prefab";

        GameObject root = new GameObject("ItemSlotUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(ItemSlotUI));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(74f, 74f);

        // 1. 빈 슬롯 배경
        GameObject emptyBgGo = CreateUIChild(root, "empty_slot_bg", Vector2.zero, Vector2.one, Vector2.zero);
        Image emptyBg = emptyBgGo.AddComponent<Image>();
        emptyBg.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300008, "8");
        emptyBg.type = Image.Type.Sliced;
        emptyBg.color = new Color(0.25f, 0.20f, 0.16f, 1f);

        // 2. 활성 테두리 + 유리광
        GameObject activeFrameGo = CreateUIChild(root, "active_frame", Vector2.zero, Vector2.one, Vector2.zero);
        Image activeFrameImg = activeFrameGo.AddComponent<Image>();
        activeFrameImg.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300138, "138");
        activeFrameImg.type = Image.Type.Sliced;
        activeFrameImg.color = Color.white;

        GameObject glassGo = CreateUIChild(activeFrameGo, "glass_reflection", Vector2.zero, Vector2.one, new Vector2(-4f, -4f));
        Image glassImg = glassGo.AddComponent<Image>();
        glassImg.raycastTarget = false;
        glassImg.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300008, "8");
        glassImg.type = Image.Type.Sliced;
        glassImg.color = new Color(1f, 1f, 1f, 0.45f);

        activeFrameGo.SetActive(false);

        // 3. 아이콘 (기본 비활성화)
        GameObject iconGo = CreateUIChild(root, "icon", Vector2.zero, Vector2.one, new Vector2(-12f, -12f));
        Image iconImg = iconGo.AddComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;
        iconImg.sprite = null;
        iconGo.SetActive(false);

        // 4. 수량 텍스트
        GameObject countGo = CreateUIChild(root, "item_count", new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-6f, 18f));
        countGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-4f, 4f);
        TextMeshProUGUI countText = countGo.AddComponent<TextMeshProUGUI>();
        if (s_font != null) countText.font = s_font;
        countText.alignment = TextAlignmentOptions.BottomRight;
        countText.fontSize = 13;
        countText.textWrappingMode = TextWrappingModes.NoWrap;
        countText.overflowMode = TextOverflowModes.Overflow;
        countText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        countGo.SetActive(false);

        // 5. 장착 마커 (E)
        GameObject equipGo = CreateUIChild(root, "equip_marker", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, 20f));
        equipGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(12f, -12f);
        TextMeshProUGUI equipText = equipGo.AddComponent<TextMeshProUGUI>();
        if (s_font != null) equipText.font = s_font;
        equipText.text = "E";
        equipText.fontSize = 15;
        equipText.fontStyle = FontStyles.Bold;
        equipText.textWrappingMode = TextWrappingModes.NoWrap;
        equipText.color = new Color(1f, 0.84f, 0.2f, 1f);
        equipGo.SetActive(false);

        // 6. 하이라이트 아웃라인
        GameObject hlGo = CreateUIChild(root, "highlight_outline", Vector2.zero, Vector2.one, Vector2.zero);
        Image hlImg = hlGo.AddComponent<Image>();
        hlImg.raycastTarget = false;
        hlImg.type = Image.Type.Sliced;
        hlImg.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300094, "94");
        hlImg.color = new Color(1f, 0.85f, 0.4f, 0.7f);
        hlGo.SetActive(false);

        SerializedObject so = new SerializedObject(root.GetComponent<ItemSlotUI>());
        so.FindProperty("_iconImage").objectReferenceValue = iconImg;
        so.FindProperty("_amountText").objectReferenceValue = countText;
        so.FindProperty("_equipMarker").objectReferenceValue = equipGo;
        so.FindProperty("_highlightOutline").objectReferenceValue = hlGo;
        so.FindProperty("_activeFrame").objectReferenceValue = activeFrameGo;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildEquipSlotPrefab()
    {
        string path = $"{SubPrefabPath}/EquipSlotUI.prefab";

        GameObject baseSlot = BuildItemSlotPrefab();
        GameObject root = Object.Instantiate(baseSlot);
        root.name = "EquipSlotUI";
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(72f, 72f);

        GameObject phGo = CreateUIChild(root, "placeholder_icon", Vector2.zero, Vector2.one, new Vector2(-14f, -14f));
        phGo.transform.SetSiblingIndex(1);
        Image phImg = phGo.AddComponent<Image>();
        phImg.raycastTarget = false;
        phImg.preserveAspect = true;
        phImg.color = new Color(0.9f, 0.85f, 0.75f, 0.45f);

        SerializedObject so = new SerializedObject(root.GetComponent<ItemSlotUI>());
        so.FindProperty("_placeholderIcon").objectReferenceValue = phImg;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildQuickSlotItemPrefab()
    {
        string path = $"{SubPrefabPath}/QuickSlotItemUI.prefab";

        GameObject root = new GameObject("QuickSlotItemUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(QuickSlotItemUI));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(68f, 68f);

        Image bgImage = root.GetComponent<Image>();
        bgImage.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300138, "138");
        bgImage.type = Image.Type.Sliced;
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

        GameObject iconGo = CreateUIChild(root, "icon", Vector2.zero, Vector2.one, new Vector2(-14f, -14f));
        Image iconImg = iconGo.AddComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;
        iconImg.sprite = LoadSpriteByFileId(GuidIconAtlas, 21300002, "2");
        iconGo.SetActive(true);

        GameObject cdGo = CreateUIChild(root, "cooldown_radial", Vector2.zero, Vector2.one, new Vector2(-6f, -6f));
        Image cdImg = cdGo.AddComponent<Image>();
        cdImg.raycastTarget = false;
        cdImg.type = Image.Type.Filled;
        cdImg.fillMethod = Image.FillMethod.Radial360;
        cdImg.fillOrigin = (int)Image.Origin360.Top;
        cdImg.color = new Color(0f, 0f, 0f, 0.75f);
        cdImg.fillAmount = 0f;

        GameObject amtGo = CreateUIChild(root, "amount_text", new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-6f, 18f));
        amtGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-4f, 4f);
        TextMeshProUGUI amtText = amtGo.AddComponent<TextMeshProUGUI>();
        if (s_font != null) amtText.font = s_font;
        amtText.alignment = TextAlignmentOptions.BottomRight;
        amtText.fontSize = 13;
        amtText.textWrappingMode = TextWrappingModes.NoWrap;
        amtText.overflowMode = TextOverflowModes.Overflow;
        amtText.text = "10";
        amtText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        amtGo.SetActive(true);

        GameObject selGo = CreateUIChild(root, "selection_outline", Vector2.zero, Vector2.one, new Vector2(4f, 4f));
        Image selImg = selGo.AddComponent<Image>();
        selImg.raycastTarget = false;
        selImg.type = Image.Type.Sliced;
        selImg.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300094, "94");
        selImg.color = new Color(1f, 0.8f, 0.2f, 0.85f);
        selGo.SetActive(false);

        SerializedObject so = new SerializedObject(root.GetComponent<QuickSlotItemUI>());
        so.FindProperty("_iconImage").objectReferenceValue = iconImg;
        so.FindProperty("_amountText").objectReferenceValue = amtText;
        so.FindProperty("_cooldownRadialImage").objectReferenceValue = cdImg;
        so.FindProperty("_selectionOutline").objectReferenceValue = selGo;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    #endregion

    #region 2. InventoryUI.prefab (정밀 대칭 빌드)

    private static void BuildInventoryWindowPrefab(GameObject itemSlotPrefab, GameObject equipSlotPrefab)
    {
        string path = $"{WindowPrefabPath}/InventoryUI.prefab";

        GameObject root = new GameObject("InventoryUI", typeof(RectTransform), typeof(CanvasGroup), typeof(InventoryView));
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;

        // 메인 프레임 패널 (1560 x 860)
        GameObject mainPanel = CreateUIChild(root, "MainPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1560f, 860f));
        Image panelBg = mainPanel.AddComponent<Image>();
        panelBg.sprite = LoadSpriteByFileId(GuidWindowFrame, 21300000);
        panelBg.type = Image.Type.Sliced;

        // 상단 헤더 바 (높이 44px, 상단 마진 28px)
        GameObject headerBar = CreateUIChild(mainPanel, "TopHeaderBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-60f, 44f));
        headerBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -28f);
        Image hbBg = headerBar.AddComponent<Image>();
        hbBg.sprite = LoadSpriteByFileId(GuidPanelBg, 21300000);
        hbBg.type = Image.Type.Sliced;
        hbBg.color = new Color(0.18f, 0.16f, 0.14f, 0.95f);

        GameObject titleGo = CreateUIChild(headerBar, "TitleText", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, 30f));
        titleGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(110f, 0f);
        TextMeshProUGUI titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        if (s_font != null) titleTxt.font = s_font;
        titleTxt.text = "인벤토리";
        titleTxt.fontSize = 20;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.textWrappingMode = TextWrappingModes.NoWrap;
        titleTxt.overflowMode = TextOverflowModes.Overflow;
        titleTxt.color = new Color(0.95f, 0.9f, 0.8f, 1f);

        // 상단 소지 소울 표시부
        GameObject soulsContainer = CreateUIChild(headerBar, "SoulsHeader", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(260f, 30f));
        soulsContainer.GetComponent<RectTransform>().anchoredPosition = new Vector2(-150f, 0f);
        HorizontalLayoutGroup shLayout = soulsContainer.AddComponent<HorizontalLayoutGroup>();
        shLayout.childAlignment = TextAnchor.MiddleRight;
        shLayout.spacing = 10f;
        shLayout.childControlWidth = false;
        shLayout.childControlHeight = true;

        CreateDirectText(soulsContainer, "Label", "소지 소울", 15, 24f, new Color(0.8f, 0.75f, 0.65f, 1f));
        TextMeshProUGUI sValue = CreateDirectText(soulsContainer, "Value", "12,450", 17, 24f, new Color(1f, 0.84f, 0.2f, 1f));
        sValue.fontStyle = FontStyles.Bold;

        // 닫기 버튼
        GameObject closeBtnGo = CreateUIChild(mainPanel, "btn_close", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(38f, 38f));
        closeBtnGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-22f, -22f);
        Button closeBtn = closeBtnGo.AddComponent<Button>();
        Image closeImg = closeBtnGo.AddComponent<Image>();
        closeImg.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300098);

        // 3개 컬럼 컨테이너 (상하좌우 마진 완전 일치: 1460 x 746)
        GameObject columnsRoot = CreateUIChild(mainPanel, "ColumnsRoot", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1460f, 746f));
        columnsRoot.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 30f);
        HorizontalLayoutGroup hLayout = columnsRoot.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 20f;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = true;

        // 1. 좌측 열 (가방 440px)
        GameObject leftCol = CreateColumn(columnsRoot, "Left_Column", 440f);
        Image leftBg = leftCol.AddComponent<Image>();
        leftBg.sprite = LoadSpriteByFileId(GuidPanelBg, 21300000);
        leftBg.type = Image.Type.Sliced;

        VerticalLayoutGroup leftVLayout = leftCol.AddComponent<VerticalLayoutGroup>();
        leftVLayout.padding = new RectOffset(16, 16, 16, 16);
        leftVLayout.spacing = 10f;
        leftVLayout.childControlWidth = true;
        leftVLayout.childControlHeight = false;

        // 상단 5대 카테고리 탭
        GameObject tabsRoot = CreateUIChild(leftCol, "CategoryTabs", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 48f));
        tabsRoot.AddComponent<LayoutElement>().preferredHeight = 48f;
        HorizontalLayoutGroup tabsLayout = tabsRoot.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 4f;
        tabsLayout.childAlignment = TextAnchor.MiddleCenter;
        ToggleGroup toggleGroup = tabsRoot.AddComponent<ToggleGroup>();

        Toggle tabWeapon = CreateCategoryToggle(tabsRoot, "Toggle_Weapon", toggleGroup, GuidTabWeapon, 21300000, true);
        Toggle tabShield = CreateCategoryToggle(tabsRoot, "Toggle_Shield", toggleGroup, GuidIconAtlas, 21300006, false);
        Toggle tabArmor = CreateCategoryToggle(tabsRoot, "Toggle_Armor", toggleGroup, GuidTabArmor, 21300000, false);
        Toggle tabAccessory = CreateCategoryToggle(tabsRoot, "Toggle_Accessory", toggleGroup, GuidTabAccessory, 21300000, false);
        Toggle tabConsumable = CreateCategoryToggle(tabsRoot, "Toggle_Consumable", toggleGroup, GuidTabConsumable, 21300000, false);

        // 5x6 가방 스크롤뷰 (높이 500px, 6행 슬롯이 배경 안쪽으로 완벽 수납)
        GameObject scrollGo = CreateUIChild(leftCol, "Bag_ScrollView", Vector2.zero, Vector2.one, new Vector2(0f, 500f));
        scrollGo.AddComponent<LayoutElement>().preferredHeight = 500f;
        Image scrollFrame = scrollGo.AddComponent<Image>();
        scrollFrame.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300072);
        scrollFrame.type = Image.Type.Sliced;

        ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        GameObject viewport = CreateUIChild(scrollGo, "Viewport", Vector2.zero, Vector2.one, new Vector2(-12f, -12f));
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        viewport.AddComponent<Image>().color = Color.white;

        GameObject content = CreateUIChild(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f));
        content.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        GridLayoutGroup gridLayout = content.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(72f, 72f);
        gridLayout.spacing = new Vector2(6f, 6f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 5;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content.GetComponent<RectTransform>();

        List<ItemSlotUI> gridSlotList = new List<ItemSlotUI>(30);
        for (int i = 0; i < 30; i++)
        {
            GameObject slotInstance = PrefabUtility.InstantiatePrefab(itemSlotPrefab, content.transform) as GameObject;
            slotInstance.name = $"Slot_{i:D2}";
            gridSlotList.Add(slotInstance.GetComponent<ItemSlotUI>());
        }

        // 최하단 퀵슬롯 등록 바 (4칸, 높이 74px)
        GameObject quickBarRoot = CreateUIChild(leftCol, "QuickAssignBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 74f));
        quickBarRoot.AddComponent<LayoutElement>().preferredHeight = 74f;
        HorizontalLayoutGroup qbLayout = quickBarRoot.AddComponent<HorizontalLayoutGroup>();
        qbLayout.spacing = 14f;
        qbLayout.childAlignment = TextAnchor.MiddleCenter;

        ItemSlotUI[] quickSlotList = new ItemSlotUI[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject qInstance = PrefabUtility.InstantiatePrefab(itemSlotPrefab, quickBarRoot.transform) as GameObject;
            qInstance.name = $"QuickAssign_{i}";
            quickSlotList[i] = qInstance.GetComponent<ItemSlotUI>();
        }

        // 2. 중앙 열 (상세 정보창 540px)
        GameObject centerCol = CreateColumn(columnsRoot, "Center_Column", 540f);
        Image centerBg = centerCol.AddComponent<Image>();
        centerBg.sprite = LoadSpriteByFileId(GuidPanelBg, 21300000);
        centerBg.type = Image.Type.Sliced;
        ItemDetailPaneUI detailPane = centerCol.AddComponent<ItemDetailPaneUI>();
        BuildCenterDetailPane(centerCol, detailPane);

        // 3. 우측 열 (8대 장비 슬롯 + 콤팩트 스탯 패널 440px)
        GameObject rightCol = CreateColumn(columnsRoot, "Right_Column", 440f);
        Image rightBg = rightCol.AddComponent<Image>();
        rightBg.sprite = LoadSpriteByFileId(GuidPanelBg, 21300000);
        rightBg.type = Image.Type.Sliced;
        CharacterDataPaneUI charPane = rightCol.AddComponent<CharacterDataPaneUI>();
        ItemSlotUI[] equipSlots = BuildRightCharacterPane(rightCol, equipSlotPrefab, charPane);

        // InventoryView 직렬화 바인딩
        SerializedObject viewSo = new SerializedObject(root.GetComponent<InventoryView>());
        viewSo.FindProperty("_currentSoulsText").objectReferenceValue = sValue;
        viewSo.FindProperty("_closeButton").objectReferenceValue = closeBtn;

        SerializedProperty gridProp = viewSo.FindProperty("_gridSlots");
        gridProp.ClearArray();
        for (int i = 0; i < gridSlotList.Count; i++)
        {
            gridProp.InsertArrayElementAtIndex(i);
            gridProp.GetArrayElementAtIndex(i).objectReferenceValue = gridSlotList[i];
        }

        // 지정 순서 바인딩: 1번 무기, 2번 방패, 3번 장갑, 4번 신발, 5번 헬맷, 6번 갑옷, 7번 목걸이, 8번 반지
        viewSo.FindProperty("_mainWeaponSlot").objectReferenceValue = equipSlots[0];
        viewSo.FindProperty("_shieldSlot").objectReferenceValue = equipSlots[1];
        viewSo.FindProperty("_gauntletsSlot").objectReferenceValue = equipSlots[2];
        viewSo.FindProperty("_bootsSlot").objectReferenceValue = equipSlots[3];
        viewSo.FindProperty("_helmetSlot").objectReferenceValue = equipSlots[4];
        viewSo.FindProperty("_chestArmorSlot").objectReferenceValue = equipSlots[5];
        viewSo.FindProperty("_necklaceSlot").objectReferenceValue = equipSlots[6];
        viewSo.FindProperty("_ringSlot").objectReferenceValue = equipSlots[7];

        SerializedProperty quickProp = viewSo.FindProperty("_quickAssignSlots");
        for (int i = 0; i < 4; i++) quickProp.GetArrayElementAtIndex(i).objectReferenceValue = quickSlotList[i];

        viewSo.FindProperty("_weaponTabToggle").objectReferenceValue = tabWeapon;
        viewSo.FindProperty("_shieldTabToggle").objectReferenceValue = tabShield;
        viewSo.FindProperty("_armorTabToggle").objectReferenceValue = tabArmor;
        viewSo.FindProperty("_accessoryTabToggle").objectReferenceValue = tabAccessory;
        viewSo.FindProperty("_consumableTabToggle").objectReferenceValue = tabConsumable;
        viewSo.FindProperty("_categoryToggleGroup").objectReferenceValue = toggleGroup;

        viewSo.FindProperty("_detailPaneUI").objectReferenceValue = detailPane;
        viewSo.FindProperty("_characterDataPaneUI").objectReferenceValue = charPane;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static GameObject CreateColumn(GameObject parent, string name, float width)
    {
        GameObject col = CreateUIChild(parent, name, Vector2.zero, Vector2.one, Vector2.zero);
        LayoutElement le = col.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
        le.flexibleWidth = 0;
        return col;
    }

    private static Toggle CreateCategoryToggle(GameObject parent, string name, ToggleGroup group, string iconGuid, long iconFileId, bool isOn)
    {
        GameObject toggleGo = CreateUIChild(parent, name, Vector2.zero, Vector2.one, new Vector2(48f, 48f));
        Toggle toggle = toggleGo.AddComponent<Toggle>();
        toggle.group = group;
        toggle.isOn = isOn;

        GameObject bgGo = CreateUIChild(toggleGo, "Background", Vector2.zero, Vector2.one, Vector2.zero);
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300072);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.25f, 0.22f, 0.18f, 1f);

        GameObject checkGo = CreateUIChild(bgGo, "Checkmark", Vector2.zero, Vector2.one, new Vector2(-6f, -6f));
        Image checkImg = checkGo.AddComponent<Image>();
        checkImg.sprite = LoadSpriteByFileId(GuidPanelBg, 21300008);
        checkImg.type = Image.Type.Sliced;
        checkImg.color = new Color(1f, 0.84f, 0f, 0.75f);

        GameObject iconGo = CreateUIChild(bgGo, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 30f));
        Image iconImg = iconGo.AddComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;
        iconImg.sprite = LoadSpriteByFileId(iconGuid, iconFileId);
        iconImg.color = new Color(0.95f, 0.85f, 0.65f, 1f);

        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;

        return toggle;
    }

    private static void BuildCenterDetailPane(GameObject parent, ItemDetailPaneUI comp)
    {
        VerticalLayoutGroup vLayout = parent.AddComponent<VerticalLayoutGroup>();
        vLayout.padding = new RectOffset(20, 20, 16, 16);
        vLayout.spacing = 8f;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;

        // 공통 헤더
        GameObject header = CreateUIChild(parent, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 75f));
        header.AddComponent<LayoutElement>().preferredHeight = 75f;

        GameObject iconGo = CreateUIChild(header, "ItemIcon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(65f, 65f));
        Image iconImg = iconGo.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.sprite = null;

        GameObject nameGo = CreateUIChild(header, "ItemName", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-80f, 35f));
        nameGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(80f, 0f);
        TextMeshProUGUI nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
        if (s_font != null) nameTxt.font = s_font;
        nameTxt.text = "아이템을 선택하십시오";
        nameTxt.fontSize = 19;
        nameTxt.textWrappingMode = TextWrappingModes.NoWrap;
        nameTxt.overflowMode = TextOverflowModes.Overflow;
        nameTxt.color = new Color(1f, 0.85f, 0.6f, 1f);

        GameObject catGo = CreateUIChild(header, "CategoryText", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-80f, 25f));
        catGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(80f, 0f);
        TextMeshProUGUI catTxt = catGo.AddComponent<TextMeshProUGUI>();
        if (s_font != null) catTxt.font = s_font;
        catTxt.fontSize = 13;
        catTxt.textWrappingMode = TextWrappingModes.NoWrap;
        catTxt.overflowMode = TextOverflowModes.Overflow;
        catTxt.color = Color.gray;

        // 1. 소모품 전용 서브 패널
        GameObject consPanel = CreateUIChild(parent, "ConsumableSubPanel", Vector2.zero, Vector2.one, Vector2.zero);
        VerticalLayoutGroup cpLayout = consPanel.AddComponent<VerticalLayoutGroup>();
        cpLayout.spacing = 8f;
        cpLayout.childControlWidth = true;
        cpLayout.childControlHeight = false;

        GameObject countsGo = CreateUIChild(consPanel, "Counts", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 24f));
        countsGo.AddComponent<LayoutElement>().preferredHeight = 24f;
        TextMeshProUGUI possTxt = CreateDirectText(countsGo, "PossessionCount", "소지수: - / -", 13, 24f, Color.white);
        TextMeshProUGUI storTxt = CreateDirectText(countsGo, "StorageCount", "저장수: - / -", 13, 24f, Color.white);

        GameObject effectBox = CreateUIChild(consPanel, "EffectBox", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 70f));
        effectBox.AddComponent<LayoutElement>().preferredHeight = 70f;
        Image effBg = effectBox.AddComponent<Image>();
        effBg.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300072);
        effBg.type = Image.Type.Sliced;
        effBg.color = new Color(0.2f, 0.18f, 0.15f, 0.9f);
        TextMeshProUGUI effTxt = CreateDirectText(effectBox, "EffectSummary", "아이템 효과: HP를 회복한다", 13, 60f, new Color(0.9f, 0.9f, 0.9f, 1f));

        GameObject consDescGo = CreateUIChild(consPanel, "ConsumableDesc", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 360f));
        consDescGo.AddComponent<LayoutElement>().preferredHeight = 360f;
        TextMeshProUGUI consDescTxt = consDescGo.AddComponent<TextMeshProUGUI>();
        if (s_font != null) consDescTxt.font = s_font;
        consDescTxt.fontSize = 13;
        consDescTxt.lineSpacing = -6f;
        consDescTxt.text = "아이템에 대한 전승과 효과 설명이 출력됩니다.";

        // 2. 장비 전용 서브 패널 (내구도 제거, 콤팩트 패널 구성)
        GameObject equipPanel = CreateUIChild(parent, "EquipmentSubPanel", Vector2.zero, Vector2.one, Vector2.zero);
        VerticalLayoutGroup epLayout = equipPanel.AddComponent<VerticalLayoutGroup>();
        epLayout.spacing = 8f;
        epLayout.childControlWidth = true;
        epLayout.childControlHeight = false;

        TextMeshProUGUI wtTxt = CreateDirectText(equipPanel, "WeightLine", "중량 16.5", 13, 22f, Color.yellow);

        GameObject specTable = CreateUIChild(equipPanel, "SpecTable", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 130f));
        specTable.AddComponent<LayoutElement>().preferredHeight = 130f;
        Image stBg = specTable.AddComponent<Image>();
        stBg.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300072);
        stBg.type = Image.Type.Sliced;
        stBg.color = new Color(0.18f, 0.16f, 0.14f, 0.95f);

        HorizontalLayoutGroup stLayout = specTable.AddComponent<HorizontalLayoutGroup>();
        stLayout.padding = new RectOffset(14, 14, 8, 8);
        stLayout.spacing = 15f;
        stLayout.childControlWidth = true;

        TextMeshProUGUI atkTxt = CreateDirectText(specTable, "AttackPower", "공격력\n물리: 248\n치명: 100", 13, 110f, Color.white);
        atkTxt.lineSpacing = -6f;
        TextMeshProUGUI grdTxt = CreateDirectText(specTable, "GuardAbsorption", "가드 시 감소율\n물리: 52.2\n버티기: 42", 13, 110f, Color.white);
        grdTxt.lineSpacing = -6f;

        TextMeshProUGUI sclTxt = CreateDirectText(equipPanel, "Scaling", "능력 보정: 근력 D   기량 C", 13, 22f, Color.white);
        TextMeshProUGUI reqTxt = CreateDirectText(equipPanel, "Requirements", "필요 능력치: 근력 24   기량 16", 13, 22f, Color.white);

        GameObject equipDescGo = CreateUIChild(equipPanel, "EquipmentDesc", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 260f));
        equipDescGo.AddComponent<LayoutElement>().preferredHeight = 260f;
        TextMeshProUGUI equipDescTxt = equipDescGo.AddComponent<TextMeshProUGUI>();
        if (s_font != null) equipDescTxt.font = s_font;
        equipDescTxt.fontSize = 13;
        equipDescTxt.lineSpacing = -6f;
        equipDescTxt.text = "장비에 대한 설명이 출력됩니다.";

        consPanel.SetActive(false);
        equipPanel.SetActive(false);

        SerializedObject so = new SerializedObject(comp);
        so.FindProperty("_itemIcon").objectReferenceValue = iconImg;
        so.FindProperty("_itemNameText").objectReferenceValue = nameTxt;
        so.FindProperty("_categoryText").objectReferenceValue = catTxt;
        so.FindProperty("_consumablePanel").objectReferenceValue = consPanel;
        so.FindProperty("_equipmentPanel").objectReferenceValue = equipPanel;
        so.FindProperty("_possessionCountText").objectReferenceValue = possTxt;
        so.FindProperty("_storageCountText").objectReferenceValue = storTxt;
        so.FindProperty("_effectSummaryText").objectReferenceValue = effTxt;
        so.FindProperty("_consumableDescText").objectReferenceValue = consDescTxt;
        so.FindProperty("_equipWeightText").objectReferenceValue = wtTxt;
        so.FindProperty("_attackPowerText").objectReferenceValue = atkTxt;
        so.FindProperty("_guardAbsorptionText").objectReferenceValue = grdTxt;
        so.FindProperty("_scalingGradeText").objectReferenceValue = sclTxt;
        so.FindProperty("_requirementsText").objectReferenceValue = reqTxt;
        so.FindProperty("_equipmentDescText").objectReferenceValue = equipDescTxt;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ItemSlotUI[] BuildRightCharacterPane(GameObject parent, GameObject equipSlotPrefab, CharacterDataPaneUI comp)
    {
        VerticalLayoutGroup vLayout = parent.AddComponent<VerticalLayoutGroup>();
        vLayout.padding = new RectOffset(16, 16, 16, 16);
        vLayout.spacing = 10f;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;

        // 상단: 8대 장비 슬롯 (160px 높이에 4x2 대칭 배치)
        GameObject equipRoot = CreateUIChild(parent, "EquipSlotsRoot", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 160f));
        equipRoot.AddComponent<LayoutElement>().preferredHeight = 160f;

        ItemSlotUI[] slots = new ItemSlotUI[8];
        Vector2[] slotPositions = new Vector2[]
        {
            new Vector2(-126f,  40f), // 1번 : 무기 (MainWeapon, 검)
            new Vector2( -42f,  40f), // 2번 : 방패 (Shield)
            new Vector2(-126f, -40f), // 3번 : 장갑 (Gauntlets)
            new Vector2( -42f, -40f), // 4번 : 신발 (Boots)
            new Vector2(  42f,  40f), // 5번 : 헬맷 (Helmet)
            new Vector2( 126f,  40f), // 6번 : 갑옷 (ChestArmor)
            new Vector2(  42f, -40f), // 7번 : 목걸이 (Necklace)
            new Vector2( 126f, -40f)  // 8번 : 반지 (Ring)
        };

        string[] slotNames = new string[] {
            "Slot_MainWeapon", "Slot_Shield", "Slot_Gauntlets", "Slot_Boots",
            "Slot_Helmet", "Slot_ChestArmor", "Slot_Necklace", "Slot_Ring"
        };

        long[] equipSilhouetteFileIds = new long[]
        {
            21300004, // 1번 검
            21300006, // 2번 방패
            21300012, // 3번 장갑
            21300010, // 4번 신발
            21300008, // 5번 헬맷
            21300014, // 6번 갑옷
            21300028, // 7번 목걸이
            21300026  // 8번 반지
        };

        for (int i = 0; i < 8; i++)
        {
            GameObject slotInstance = PrefabUtility.InstantiatePrefab(equipSlotPrefab, equipRoot.transform) as GameObject;
            slotInstance.name = slotNames[i];
            RectTransform sRt = slotInstance.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.5f, 0.5f);
            sRt.anchorMax = new Vector2(0.5f, 0.5f);
            sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.anchoredPosition = slotPositions[i];

            Transform phTr = slotInstance.transform.Find("placeholder_icon");
            if (phTr != null && phTr.TryGetComponent(out Image phImg))
            {
                phImg.sprite = LoadSpriteByFileId(GuidIconAtlas, equipSilhouetteFileIds[i]);
                phImg.gameObject.SetActive(true);
            }

            slots[i] = slotInstance.GetComponent<ItemSlotUI>();
        }

        // [화면 범위 초과 해결] 컴팩트 캐릭터 스탯 패널 (높이 420px, 텍스트 높이 24px, Spacing 2px)
        GameObject statusPanel = CreateUIChild(parent, "CharacterStatusPanel", Vector2.zero, Vector2.one, new Vector2(0f, 420f));
        statusPanel.AddComponent<LayoutElement>().preferredHeight = 420f;
        Image spBg = statusPanel.AddComponent<Image>();
        spBg.sprite = LoadSpriteByFileId(GuidSlotAtlas, 21300072);
        spBg.type = Image.Type.Sliced;
        spBg.color = new Color(0.18f, 0.16f, 0.14f, 0.95f);

        VerticalLayoutGroup spLayout = statusPanel.AddComponent<VerticalLayoutGroup>();
        spLayout.padding = new RectOffset(16, 16, 10, 10);
        spLayout.spacing = 2f; // [핵심] 간격을 2px로 압축하여 13개 텍스트 전체가 패널 내부에 완벽 수납
        spLayout.childControlWidth = true;
        spLayout.childControlHeight = false;

        TextMeshProUGUI lvlTxt = CreateDirectText(statusPanel, "SoulLevel", "레벨: 1", 13, 24f, Color.white);
        TextMeshProUGUI hpTxt = CreateDirectText(statusPanel, "HP", "HP: 650 / 650", 12.5f, 24f, Color.white);
        TextMeshProUGUI stamTxt = CreateDirectText(statusPanel, "Stamina", "스태미너: 100 / 100", 12.5f, 24f, Color.white);
        TextMeshProUGUI vigTxt = CreateDirectText(statusPanel, "Vigor", "생명력: 10", 12.5f, 24f, Color.white);
        TextMeshProUGUI endTxt = CreateDirectText(statusPanel, "Endurance", "지구력: 10", 12.5f, 24f, Color.white);
        TextMeshProUGUI strTxt = CreateDirectText(statusPanel, "Strength", "근력: 10 (+0)", 12.5f, 24f, Color.white);
        TextMeshProUGUI dexTxt = CreateDirectText(statusPanel, "Dexterity", "기량: 10 (+0)", 12.5f, 24f, Color.white);
        TextMeshProUGUI arTxt = CreateDirectText(statusPanel, "AttackPower", "공격력: 110", 12.5f, 24f, Color.white);
        TextMeshProUGUI defTxt = CreateDirectText(statusPanel, "Defense", "물리 방어력: 85", 12.5f, 24f, Color.white);
        TextMeshProUGUI absTxt = CreateDirectText(statusPanel, "Absorption", "경감률: 18.5%", 12.5f, 24f, Color.white);
        TextMeshProUGUI poiseTxt = CreateDirectText(statusPanel, "Poise", "강인도: 25.0", 12.5f, 24f, Color.white);
        TextMeshProUGUI wtTxt = CreateDirectText(statusPanel, "EquipWeight", "장비 중량: 24.5 / 52.0", 12.5f, 24f, Color.white);
        TextMeshProUGUI ratioTxt = CreateDirectText(statusPanel, "WeightRatio", "47.1% (보통)", 12.5f, 24f, Color.white);

        SerializedObject so = new SerializedObject(comp);
        so.FindProperty("_soulLevelText").objectReferenceValue = lvlTxt;
        so.FindProperty("_vigorText").objectReferenceValue = vigTxt;
        so.FindProperty("_enduranceText").objectReferenceValue = endTxt;
        so.FindProperty("_strengthText").objectReferenceValue = strTxt;
        so.FindProperty("_dexterityText").objectReferenceValue = dexTxt;
        so.FindProperty("_hpText").objectReferenceValue = hpTxt;
        so.FindProperty("_staminaText").objectReferenceValue = stamTxt;
        so.FindProperty("_equipWeightText").objectReferenceValue = wtTxt;
        so.FindProperty("_weightRatioText").objectReferenceValue = ratioTxt;
        so.FindProperty("_attackPowerText").objectReferenceValue = arTxt;
        so.FindProperty("_defenseText").objectReferenceValue = defTxt;
        so.FindProperty("_absorptionText").objectReferenceValue = absTxt;
        so.FindProperty("_poiseText").objectReferenceValue = poiseTxt;
        so.ApplyModifiedPropertiesWithoutUndo();

        return slots;
    }

    #endregion

    #region 3. QuickSlotHUD.prefab 빌드

    private static void BuildQuickSlotHudPrefab(GameObject quickSlotItemPrefab)
    {
        string path = $"{HudPrefabPath}/QuickSlotHUD.prefab";

        GameObject root = new GameObject("QuickSlotHUD", typeof(RectTransform), typeof(QuickSlotView));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(40f, 40f);
        rt.sizeDelta = new Vector2(340f, 90f);

        Image panelBg = root.AddComponent<Image>();
        panelBg.sprite = LoadSpriteByFileId(GuidPanelBg, 21300002);
        panelBg.type = Image.Type.Sliced;

        HorizontalLayoutGroup hLayout = root.AddComponent<HorizontalLayoutGroup>();
        hLayout.padding = new RectOffset(10, 10, 10, 10);
        hLayout.spacing = 10f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;

        QuickSlotItemUI[] slotUIs = new QuickSlotItemUI[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject qInstance = PrefabUtility.InstantiatePrefab(quickSlotItemPrefab, root.transform) as GameObject;
            qInstance.name = $"Slot_{i}";
            slotUIs[i] = qInstance.GetComponent<QuickSlotItemUI>();
        }

        SerializedObject so = new SerializedObject(root.GetComponent<QuickSlotView>());
        SerializedProperty slotsProp = so.FindProperty("_slotUIs");
        for (int i = 0; i < 4; i++) slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotUIs[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    #endregion

    #region 유틸리티

    private static GameObject CreateUIChild(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = sizeDelta;
        return go;
    }

    private static TextMeshProUGUI CreateDirectText(GameObject parent, string name, string text, float fontSize, float height, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, height);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (s_font != null) tmp.font = s_font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    #endregion
}
#endif