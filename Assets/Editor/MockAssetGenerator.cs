#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유니티 기본 UGUI 및 파티클을 활용해 부트스트랩 필수 프리팹을 1초 만에 자동 생성하고
/// Addressables 그룹에 해당 키로 즉시 등록해 주는 에디터 자동화 툴
/// </summary>
public static class MockAssetGenerator
{
    private const string PrefabSaveDirectory = "Assets/03.Prefabs/Mock";

    [MenuItem("Tools/어센던트/Generate Mock Prefabs & Setup Addressables")]
    public static void GenerateAllMockAssets()
    {
        EnsureDirectory();

        // 1. TitleMenuUI 프리팹 생성 및 등록
        CreateTitleMenuUIPrefab();

        // 2. PlayerHUD 프리팹 생성 및 등록
        CreatePlayerHUDPrefab();

        // 3. HitVFX_Slash 파티클 프리팹 생성 및 등록
        CreateHitVFXPrefab();

        // 4. FloatingHpBar 프리팹 생성 및 등록
        CreateFloatingHpBarPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        DebugLogger.LogWithTag("MockAssetGenerator", "모든 Mock 프리팹 생성 및 Addressables 등록이 완료되었습니다.");
    }

    private static void EnsureDirectory()
    {
        if (!Directory.Exists(PrefabSaveDirectory))
        {
            Directory.CreateDirectory(PrefabSaveDirectory);
            AssetDatabase.Refresh();
        }
    }

    #region 1. TitleMenuUI 제작
    private static void CreateTitleMenuUIPrefab()
    {
        string assetPath = $"{PrefabSaveDirectory}/TitleMenuUI.prefab";

        GameObject root = new GameObject("TitleMenuUI", typeof(RectTransform), typeof(CanvasGroup), typeof(TitleMenuView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(1920, 1080);

        // 배경 패널
        GameObject panelGo = CreateUIObject("BackgroundPanel", root.transform);
        Image panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
        SetFullStretch(panelGo.GetComponent<RectTransform>());

        // 버튼 세로 정렬 컨테이너
        GameObject btnContainer = CreateUIObject("ButtonContainer", root.transform);
        VerticalLayoutGroup layout = btnContainer.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = false;

        RectTransform containerRect = btnContainer.GetComponent<RectTransform>();
        containerRect.anchoredPosition = new Vector2(0, -100);
        containerRect.sizeDelta = new Vector2(400, 400);

        Button newGameBtn = CreateMockButton("Btn_NewGame", "새 게임", btnContainer.transform);
        Button continueBtn = CreateMockButton("Btn_Continue", "이어하기", btnContainer.transform);
        Button quitBtn = CreateMockButton("Btn_Quit", "게임 종료", btnContainer.transform);

        // TitleMenuView 직렬화 바인딩
        TitleMenuView view = root.GetComponent<TitleMenuView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("_newGameButton").objectReferenceValue = newGameBtn;
        so.FindProperty("_continueButton").objectReferenceValue = continueBtn;
        so.FindProperty("_quitButton").objectReferenceValue = quitBtn;
        so.ApplyModifiedProperties();

        SaveAndRegisterAddressable(root, assetPath, "TitleMenuUI");
    }
    #endregion

    #region 2. PlayerHUD 제작
    private static void CreatePlayerHUDPrefab()
    {
        string assetPath = $"{PrefabSaveDirectory}/PlayerHUD.prefab";

        GameObject root = new GameObject("PlayerHUD", typeof(RectTransform), typeof(CanvasGroup), typeof(PlayerHUDView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(1920, 1080);
        SetFullStretch(rootRect);

        // 좌상단 HUD 컨테이너
        GameObject hudContainer = CreateUIObject("TopLeft_Container", root.transform);
        RectTransform hudRect = hudContainer.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0, 1);
        hudRect.anchorMax = new Vector2(0, 1);
        hudRect.pivot = new Vector2(0, 1);
        hudRect.anchoredPosition = new Vector2(60, -50);
        hudRect.sizeDelta = new Vector2(500, 100);

        Slider hpSlider = CreateMockSlider("HP_Bar", new Color(0.85f, 0.15f, 0.15f), hudContainer.transform, new Vector2(0, 0));
        Slider staminaSlider = CreateMockSlider("Stamina_Bar", new Color(0.15f, 0.75f, 0.2f), hudContainer.transform, new Vector2(0, -35));

        // PlayerHUDView 직렬화 바인딩
        PlayerHUDView view = root.GetComponent<PlayerHUDView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("_hpSlider").objectReferenceValue = hpSlider;
        so.FindProperty("_staminaSlider").objectReferenceValue = staminaSlider;
        so.ApplyModifiedProperties();

        SaveAndRegisterAddressable(root, assetPath, "PlayerHUD");
    }
    #endregion

    #region 3. HitVFX_Slash 제작 (기본 파티클)
    private static void CreateHitVFXPrefab()
    {
        string assetPath = $"{PrefabSaveDirectory}/HitVFX_Slash.prefab";

        GameObject root = new GameObject("HitVFX_Slash", typeof(ParticleSystem), typeof(VFXPoolable));
        ParticleSystem ps = root.GetComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = 0.25f;
        main.startSpeed = 8.0f;
        main.startSize = 0.35f;
        main.startColor = new Color(1f, 0.85f, 0.3f, 1f); // 황색 스파크
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        VFXPoolable vfx = root.GetComponent<VFXPoolable>();
        SerializedObject so = new SerializedObject(vfx);
        so.FindProperty("_poolType").intValue = (int)PoolType.HitVFX_Slash;
        so.FindProperty("_particleSystem").objectReferenceValue = ps;
        so.FindProperty("_fallbackDuration").floatValue = 0.5f;
        so.ApplyModifiedProperties();

        SaveAndRegisterAddressable(root, assetPath, "HitVFX_Slash");
    }
    #endregion

    #region 4. FloatingHpBar 제작
    private static void CreateFloatingHpBarPrefab()
    {
        string assetPath = $"{PrefabSaveDirectory}/FloatingHpBar.prefab";

        GameObject root = new GameObject("FloatingHpBar", typeof(RectTransform), typeof(CanvasGroup), typeof(UIBase));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(120, 14);

        Slider hpSlider = CreateMockSlider("Slider", new Color(0.9f, 0.2f, 0.2f), root.transform, Vector2.zero);
        RectTransform sliderRect = hpSlider.GetComponent<RectTransform>();
        SetFullStretch(sliderRect);

        SaveAndRegisterAddressable(root, assetPath, "FloatingHpBar");
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

    private static Button CreateMockButton(string name, string labelText, Transform parent)
    {
        GameObject btnGo = CreateUIObject(name, parent);
        btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 50);

        Image img = btnGo.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        Button btn = btnGo.AddComponent<Button>();

        GameObject textGo = CreateUIObject("Text", btnGo.transform);
        Text text = textGo.AddComponent<Text>();
        text.text = labelText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        SetFullStretch(textGo.GetComponent<RectTransform>());

        return btn;
    }

    private static Slider CreateMockSlider(string name, Color fillColor, Transform parent, Vector2 pos)
    {
        GameObject sliderGo = CreateUIObject(name, parent);
        RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
        sliderRect.anchoredPosition = pos;
        sliderRect.sizeDelta = new Vector2(350, 18);

        // Background
        GameObject bgGo = CreateUIObject("Background", sliderGo.transform);
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        SetFullStretch(bgGo.GetComponent<RectTransform>());

        // Fill Area
        GameObject fillArea = CreateUIObject("Fill Area", sliderGo.transform);
        SetFullStretch(fillArea.GetComponent<RectTransform>());

        // Fill
        GameObject fillGo = CreateUIObject("Fill", fillArea.transform);
        Image fillImg = fillGo.AddComponent<Image>();
        fillImg.color = fillColor;
        SetFullStretch(fillGo.GetComponent<RectTransform>());

        Slider slider = sliderGo.AddComponent<Slider>();
        slider.fillRect = fillGo.GetComponent<RectTransform>();
        slider.targetGraphic = bgImg;
        slider.value = 1.0f;

        return slider;
    }

    private static void SaveAndRegisterAddressable(GameObject instance, string assetPath, string addressKey)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
        Object.DestroyImmediate(instance);

        // Addressables Settings에 자동 등록
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            AddressableAssetGroup group = settings.DefaultGroup;
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = addressKey;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }
    }
    #endregion
}
#endif