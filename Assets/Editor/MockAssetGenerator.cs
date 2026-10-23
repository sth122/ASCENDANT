#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 잔상 슬라이더(Ghost Bar)가 포함된 PlayerHUD, FloatingHpBar와
/// ESC 시스템 메뉴(SystemMenuUI)를 1초 만에 자동 생성하고 Addressables에 등록하는 에디터 툴
/// </summary>
public static class MockAssetGenerator
{
    private const string PrefabSaveDirectory = "Assets/02.Prefabs/UI";

    [MenuItem("Tools/어센던트/Generate Mock Prefabs & Setup Addressables")]
    public static void GenerateAllMockAssets()
    {
        EnsureDirectory();

        // 1. 소울라이크 잔상 & Poise가 포함된 PlayerHUD 생성
        CreatePlayerHUDPrefab();

        // 2. 적 머리 위 플로팅 HUD (HP 잔상 + Poise 그로기) 생성
        CreateEnemyFloatingHpBarPrefab();

        // 3. ESC 시스템 메뉴 창 (SystemMenuUI) 생성
        CreateSystemMenuUIPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        DebugLogger.LogWithTag("MockAssetGenerator", "모든 신규 UI 프리팹 생성 및 Addressables 등록이 완료되었습니다.");
    }

    private static void EnsureDirectory()
    {
        if (!Directory.Exists(PrefabSaveDirectory))
        {
            Directory.CreateDirectory(PrefabSaveDirectory);
            AssetDatabase.Refresh();
        }
    }

    #region 1. PlayerHUD 프리팹 생성 (HP/Stamina 잔상 + Poise)
    private static void CreatePlayerHUDPrefab()
    {
        string assetPath = $"{PrefabSaveDirectory}/PlayerHUD.prefab";

        GameObject root = new GameObject("PlayerHUD", typeof(RectTransform), typeof(CanvasGroup), typeof(PlayerHUDView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(1920, 1080);
        SetFullStretch(rootRect);

        // 좌상단 게이지 컨테이너
        GameObject containerGo = CreateUIObject("TopLeft_Gauges", root.transform);
        RectTransform containerRect = containerGo.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1);
        containerRect.anchorMax = new Vector2(0, 1);
        containerRect.pivot = new Vector2(0, 1);
        containerRect.anchoredPosition = new Vector2(60, -50);
        containerRect.sizeDelta = new Vector2(500, 120);

        // 1) HP 게이지 (후면: 주황 잔상 / 전면: 선홍색)
        CreateDoubleSlider("HP_Bar_Group", containerGo.transform, new Vector2(0, 0), new Vector2(400, 20),
            new Color(0.9f, 0.55f, 0.1f), new Color(0.85f, 0.15f, 0.15f),
            out Slider hpGhostSlider, out Slider hpFrontSlider);

        // 2) Stamina 게이지 (후면: 밝은 연두 잔상 / 전면: 진녹색)
        CreateDoubleSlider("Stamina_Bar_Group", containerGo.transform, new Vector2(0, -32), new Vector2(340, 15),
            new Color(0.7f, 0.9f, 0.3f), new Color(0.15f, 0.75f, 0.2f),
            out Slider stamGhostSlider, out Slider stamFrontSlider);

        // 3) Poise 게이지 (단일: 청록색)
        Slider poiseSlider = CreateSimpleSlider("Poise_Bar", new Color(0.18f, 0.8f, 0.85f), containerGo.transform,
            new Vector2(0, -55), new Vector2(280, 10));

        // PlayerHUDView 직렬화 필드 연결
        PlayerHUDView view = root.GetComponent<PlayerHUDView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("_hpSlider").objectReferenceValue = hpFrontSlider;
        so.FindProperty("_hpGhostSlider").objectReferenceValue = hpGhostSlider;
        so.FindProperty("_staminaSlider").objectReferenceValue = stamFrontSlider;
        so.FindProperty("_staminaGhostSlider").objectReferenceValue = stamGhostSlider;
        so.FindProperty("_poiseSlider").objectReferenceValue = poiseSlider;
        so.FindProperty("_ghostDelayDuration").floatValue = 0.45f;
        so.FindProperty("_ghostDrainSpeed").floatValue = 0.6f;
        so.ApplyModifiedProperties();

        SaveAndRegisterAddressable(root, assetPath, "PlayerHUD", "03.UI");
    }
    #endregion

    #region 2. EnemyFloatingHpBar 프리팹 생성 (HP 잔상 + Poise 그로기)
    private static void CreateEnemyFloatingHpBarPrefab()
    {
        string assetPath = $"{PrefabSaveDirectory}/FloatingHpBar.prefab";

        GameObject root = new GameObject("FloatingHpBar", typeof(RectTransform), typeof(CanvasGroup), typeof(EnemyFloatingHUDView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(140, 24);

        // 1) 적 HP 게이지 (후면 잔상 + 전면 빨강)
        CreateDoubleSlider("Enemy_HP_Group", root.transform, new Vector2(0, 4), new Vector2(140, 12),
            new Color(0.95f, 0.6f, 0.15f), new Color(0.85f, 0.1f, 0.1f),
            out Slider hpGhostSlider, out Slider hpFrontSlider);

        // 2) 적 Poise 게이지 (하단 얇은 슬라이더, 기본 골드색)
        Slider poiseSlider = CreateSimpleSlider("Enemy_Poise_Bar", new Color(0.95f, 0.75f, 0.15f), root.transform,
            new Vector2(0, -7), new Vector2(140, 6));

        // EnemyFloatingHUDView 직렬화 필드 연결
        EnemyFloatingHUDView view = root.GetComponent<EnemyFloatingHUDView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("_hpSlider").objectReferenceValue = hpFrontSlider;
        so.FindProperty("_hpGhostSlider").objectReferenceValue = hpGhostSlider;
        so.FindProperty("_poiseSlider").objectReferenceValue = poiseSlider;
        so.FindProperty("_poiseFillImage").objectReferenceValue = poiseSlider.fillRect.GetComponent<Image>();
        so.FindProperty("_worldOffset").vector3Value = new Vector3(0f, 2.2f, 0f);
        so.FindProperty("_ghostDelayDuration").floatValue = 0.4f;
        so.FindProperty("_ghostDrainSpeed").floatValue = 0.5f;
        so.ApplyModifiedProperties();

        SaveAndRegisterAddressable(root, assetPath, "FloatingHpBar", "03.UI");
    }
    #endregion

    #region 3. SystemMenuUI 프리팹 생성 (ESC 일시정지 창)
    private static void CreateSystemMenuUIPrefab()
    {
        string assetPath = $"{PrefabSaveDirectory}/SystemMenuUI.prefab";

        GameObject root = new GameObject("SystemMenuUI", typeof(RectTransform), typeof(CanvasGroup), typeof(SystemMenuView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(1920, 1080);
        SetFullStretch(rootRect);

        // 반투명 배경 딤
        GameObject dimmerGo = CreateUIObject("Dimmer", root.transform);
        Image dimImg = dimmerGo.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.75f);
        SetFullStretch(dimmerGo.GetComponent<RectTransform>());

        // 중앙 메뉴 다이얼로그 패널
        GameObject panelGo = CreateUIObject("MenuPanel", root.transform);
        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(420, 480);
        Image panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        // 타이틀 텍스트
        GameObject titleTextGo = CreateUIObject("Txt_Title", panelGo.transform);
        RectTransform titleRect = titleTextGo.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0, 170);
        titleRect.sizeDelta = new Vector2(300, 50);
        Text titleTxt = titleTextGo.AddComponent<Text>();
        titleTxt.text = "시스템 메뉴";
        titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize = 28;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color = new Color(0.9f, 0.85f, 0.7f);

        // 버튼 수직 레이아웃 컨테이너
        GameObject btnContainer = CreateUIObject("ButtonContainer", panelGo.transform);
        VerticalLayoutGroup layout = btnContainer.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = false;

        RectTransform containerRect = btnContainer.GetComponent<RectTransform>();
        containerRect.anchoredPosition = new Vector2(0, -30);
        containerRect.sizeDelta = new Vector2(350, 300);

        Button resumeBtn = CreateMockButton("Btn_Resume", "게임 재개", btnContainer.transform);
        Button titleBtn = CreateMockButton("Btn_Title", "타이틀 화면으로", btnContainer.transform);
        Button quitBtn = CreateMockButton("Btn_Quit", "게임 종료", btnContainer.transform);

        // SystemMenuView 직렬화 바인딩
        SystemMenuView view = root.GetComponent<SystemMenuView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("_resumeButton").objectReferenceValue = resumeBtn;
        so.FindProperty("_titleButton").objectReferenceValue = titleBtn;
        so.FindProperty("_quitButton").objectReferenceValue = quitBtn;
        so.FindProperty("_pauseTimeWhenOpened").boolValue = true; // 열릴 때 자동 일시정지
        so.ApplyModifiedProperties();

        SaveAndRegisterAddressable(root, assetPath, "SystemMenuUI", "03.UI");
    }
    #endregion

    #region Helper 메서드
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

    private static void CreateDoubleSlider(string groupName, Transform parent, Vector2 pos, Vector2 size,
        Color ghostColor, Color frontColor, out Slider ghostSlider, out Slider frontSlider)
    {
        GameObject groupGo = CreateUIObject(groupName, parent);
        RectTransform groupRect = groupGo.GetComponent<RectTransform>();
        groupRect.anchoredPosition = pos;
        groupRect.sizeDelta = size;

        // 1. 후면 고스트 슬라이더 (배경 포함)
        ghostSlider = CreateSliderInstance("GhostSlider", groupGo.transform, size, ghostColor, hasBackground: true);

        // 2. 전면 슬라이더 (배경 투명, Fill만 출력)
        frontSlider = CreateSliderInstance("FrontSlider", groupGo.transform, size, frontColor, hasBackground: false);
    }

    private static Slider CreateSimpleSlider(string name, Color fillColor, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject sliderGo = CreateUIObject(name, parent);
        RectTransform rect = sliderGo.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        return CreateSliderInstance("Slider", sliderGo.transform, size, fillColor, hasBackground: true);
    }

    private static Slider CreateSliderInstance(string name, Transform parent, Vector2 size, Color fillColor, bool hasBackground)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        SetFullStretch(rect);

        Image bgImg = null;
        if (hasBackground)
        {
            GameObject bgGo = CreateUIObject("Background", go.transform);
            bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
            SetFullStretch(bgGo.GetComponent<RectTransform>());
        }

        GameObject fillArea = CreateUIObject("Fill Area", go.transform);
        SetFullStretch(fillArea.GetComponent<RectTransform>());

        GameObject fillGo = CreateUIObject("Fill", fillArea.transform);
        Image fillImg = fillGo.AddComponent<Image>();
        fillImg.color = fillColor;
        SetFullStretch(fillGo.GetComponent<RectTransform>());

        Slider slider = go.AddComponent<Slider>();
        slider.fillRect = fillGo.GetComponent<RectTransform>();
        slider.targetGraphic = bgImg != null ? bgImg : fillImg;
        slider.value = 1.0f;
        slider.transition = Selectable.Transition.None;

        return slider;
    }

    private static Button CreateMockButton(string name, string labelText, Transform parent)
    {
        GameObject btnGo = CreateUIObject(name, parent);
        btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 48);

        Image img = btnGo.AddComponent<Image>();
        img.color = new Color(0.22f, 0.22f, 0.22f, 0.95f);
        Button btn = btnGo.AddComponent<Button>();

        GameObject textGo = CreateUIObject("Text", btnGo.transform);
        Text text = textGo.AddComponent<Text>();
        text.text = labelText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        SetFullStretch(textGo.GetComponent<RectTransform>());

        return btn;
    }

    private static void SaveAndRegisterAddressable(GameObject instance, string assetPath, string addressKey, string targetGroupName)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
        Object.DestroyImmediate(instance);

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null)
        {
            AddressableAssetGroup group = settings.FindGroup(targetGroupName);
            if (group == null)
            {
                group = settings.CreateGroup(targetGroupName, false, false, false, null, typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = addressKey;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }
    }
    #endregion
}
#endif