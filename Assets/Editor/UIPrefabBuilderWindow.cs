#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIPrefabBuilderWindow : EditorWindow
{
    private const string DefaultFontPath = "Assets/TextMesh Pro/Fonts/Pretendard-Regular SDF.asset";
    private const string OutputFolder = "Assets/Prefabs/UI";

    private TMP_FontAsset _pretendardFont;
    private GameObject _settingsSourcePrefab;
    private GameObject _systemMenuSourcePrefab;
    private string _statusLog = "준비 완료. 프리팹 슬롯을 확인한 뒤 빌드 버튼을 실행하세요.";

    [MenuItem("Tools/UI Builders/Build & Migrate UI Prefabs (Unity 6.3)")]
    public static void OpenWindow()
    {
        UIPrefabBuilderWindow window = GetWindow<UIPrefabBuilderWindow>("UI Prefab Builder (Unity 6.3)");
        window.minSize = new Vector2(540f, 420f);
        window.AutoLocateAssets();
        window.Show();
    }

    private void OnEnable() => AutoLocateAssets();

    private void AutoLocateAssets()
    {
        if (_pretendardFont == null)
            _pretendardFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);

        if (_settingsSourcePrefab == null)
            _settingsSourcePrefab = FindPrefabByName("window_settings")
                                 ?? AssetDatabase.LoadAssetAtPath<GameObject>($"{OutputFolder}/SettingUI.prefab");

        if (_systemMenuSourcePrefab == null)
            _systemMenuSourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{OutputFolder}/SystemMenuUI.prefab")
                                   ?? FindPrefabByName("SystemMenuUI");
    }

    private static GameObject FindPrefabByName(string filter)
    {
        string[] guids = AssetDatabase.FindAssets($"{filter} t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (System.IO.Path.GetFileNameWithoutExtension(path).Equals(filter, StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("SettingUI 슬라이더 수정 & SystemMenuUI 비율 교정 빌더", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. SettingUI: 구형 SliderManager Persistent 이벤트 제거, value(TMP) 재연결 및 비디오/오디오 2탭 정리.\n" +
            "2. SystemMenuUI: 풀스트레치 루트 + 중앙 325x368 패널 구조로 분리하여 전체 화면 덮음 현상 해결.",
            MessageType.Info);

        EditorGUILayout.Space(8);
        _pretendardFont = (TMP_FontAsset)EditorGUILayout.ObjectField("TMP Font (Pretendard)", _pretendardFont, typeof(TMP_FontAsset), false);
        _settingsSourcePrefab = (GameObject)EditorGUILayout.ObjectField("원본 설정창 프리팹 (window_settings)", _settingsSourcePrefab, typeof(GameObject), false);
        _systemMenuSourcePrefab = (GameObject)EditorGUILayout.ObjectField("SystemMenuUI 프리팹", _systemMenuSourcePrefab, typeof(GameObject), false);

        EditorGUILayout.Space(12);
        using (new EditorGUI.DisabledScope(_pretendardFont == null))
        {
            if (GUILayout.Button("1. SettingUI 슬라이더 이벤트 정리 & 재빌드", GUILayout.Height(36)))
            {
                BuildSettingPrefab();
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("2. SystemMenuUI 중앙 비율 교정 & TMP 변환", GUILayout.Height(36)))
            {
                BuildSystemMenuPrefab();
            }

            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(0.45f, 0.85f, 0.55f);
            if (GUILayout.Button("모두 실행 (1 + 2)", GUILayout.Height(42)))
            {
                if (_settingsSourcePrefab != null) BuildSettingPrefab();
                if (_systemMenuSourcePrefab != null) BuildSystemMenuPrefab();
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(_statusLog, MessageType.None);
    }

    private void EnsureOutputDirectory()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
    }

    // =========================================================================
    // 1. SettingUI.prefab 슬라이더 Persistent 이벤트 제거 및 전체 재바인딩
    // =========================================================================
    private void BuildSettingPrefab()
    {
        if (_settingsSourcePrefab == null)
        {
            EditorUtility.DisplayDialog("프리팹 필요", "원본 window_settings 또는 SettingUI 프리팹을 지정해 주세요.", "확인");
            return;
        }

        EnsureOutputDirectory();
        string destPath = $"{OutputFolder}/SettingUI.prefab";

        GameObject root = new GameObject("SettingUI", typeof(RectTransform));
        StretchToParent(root.GetComponent<RectTransform>());

        try
        {
            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            SettingView view = root.AddComponent<SettingView>();

            Image dimmer = root.AddComponent<Image>();
            dimmer.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);

            GameObject winInstance = (GameObject)PrefabUtility.InstantiatePrefab(_settingsSourcePrefab, root.transform);
            if (PrefabUtility.IsPartOfPrefabInstance(winInstance))
            {
                PrefabUtility.UnpackPrefabInstance(winInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            Transform nestedWin = FindDeepChild(winInstance.transform, "window_settings");
            if (nestedWin != null && nestedWin.gameObject != winInstance)
            {
                nestedWin.SetParent(root.transform, false);
                DestroyImmediate(winInstance);
                winInstance = nestedWin.gameObject;
            }
            winInstance.name = "window_settings";

            RectTransform winRt = winInstance.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.anchoredPosition = Vector2.zero;
            winRt.sizeDelta = new Vector2(740f, 580f);

            CleanupBrokenComponents(root);
            DestroyChildIfExists(winInstance.transform, "WindowResizeHandle");
            DestroyChildIfExists(winInstance.transform, "Toggle (13)");
            DestroyChildIfExists(winInstance.transform, "main panel game");

            ConvertLegacyTextAndDropdownToTMP(winInstance, _pretendardFont);

            foreach (Slider s in winInstance.GetComponentsInChildren<Slider>(true)) ClearPersistentSliderEvents(s);
            foreach (Button b in winInstance.GetComponentsInChildren<Button>(true)) ClearPersistentButtonEvents(b);
            foreach (Toggle t in winInstance.GetComponentsInChildren<Toggle>(true)) ClearPersistentToggleEvents(t);

            Transform videoPanelTr = FindDeepChild(winInstance.transform, "main panel video");
            Transform audioPanelTr = FindDeepChild(winInstance.transform, "main panel audio");

            // =================================================================
            // 비디오 패널 정리 및 TMP_Dropdown Template 무결성 검증/재건
            // =================================================================
            TMP_Dropdown aspectDropdown = null;
            TMP_Dropdown screenModeDropdown = null;
            Toggle bgSoundToggle = null;

            if (videoPanelTr != null)
            {
                videoPanelTr.gameObject.SetActive(true);
                FixScrollPanelFullWidth(videoPanelTr);

                Transform secondGroup = FindDeepChild(videoPanelTr, "group (2)") ?? FindDeepChild(videoPanelTr, "group (1)");
                Toggle foundBgToggle = null;
                if (secondGroup != null)
                {
                    foreach (Toggle t in secondGroup.GetComponentsInChildren<Toggle>(true))
                    {
                        if (t.GetComponentInParent<TMP_Dropdown>(true) != null) continue;

                        TextMeshProUGUI lbl = t.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (lbl != null && (lbl.text.Contains("background") || lbl.text.Contains("백그라운드")))
                        {
                            foundBgToggle = t;
                            break;
                        }
                    }
                    if (foundBgToggle == null)
                    {
                        foreach (Toggle t in secondGroup.GetComponentsInChildren<Toggle>(true))
                        {
                            if (t.GetComponentInParent<TMP_Dropdown>(true) == null)
                            {
                                foundBgToggle = t;
                                break;
                            }
                        }
                    }
                }

                Transform firstGroup = FindDeepChild(videoPanelTr, "group");
                Transform videoInnerGroup = firstGroup != null ? FindDeepChild(firstGroup, "group (4)") : null;

                if (firstGroup != null)
                {
                    Transform gTitle = FindDeepChild(firstGroup, "title");
                    if (gTitle != null && gTitle.TryGetComponent(out TextMeshProUGUI gt)) gt.text = "비디오 설정";
                }

                if (videoInnerGroup != null)
                {
                    if (foundBgToggle != null)
                    {
                        foundBgToggle.transform.SetParent(videoInnerGroup, false);
                        foundBgToggle.gameObject.SetActive(true);
                        bgSoundToggle = foundBgToggle;
                    }
                    else
                    {
                        // 이미 이전 빌드에서 편입된 경우 직접 조회
                        foreach (Toggle t in videoInnerGroup.GetComponentsInChildren<Toggle>(true))
                        {
                            if (t.GetComponentInParent<TMP_Dropdown>(true) == null)
                            {
                                bgSoundToggle = t;
                                break;
                            }
                        }
                    }

                    if (bgSoundToggle != null)
                    {
                        TextMeshProUGUI lbl = bgSoundToggle.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (lbl != null) lbl.text = "백그라운드 사운드 재생";

                        LayoutElement tle = bgSoundToggle.GetComponent<LayoutElement>() ?? bgSoundToggle.gameObject.AddComponent<LayoutElement>();
                        tle.preferredHeight = 44f;
                        tle.flexibleWidth = 1f;
                    }

                    if (secondGroup != null)
                    {
                        DestroyImmediate(secondGroup.gameObject);
                    }

                    Transform dd0 = FindDeepChild(videoInnerGroup, "dropdown_labeled");
                    if (dd0 != null)
                    {
                        dd0.gameObject.SetActive(true);
                        Transform lbl = FindDeepChild(dd0, "title");
                        if (lbl != null && lbl.TryGetComponent(out TextMeshProUGUI lt)) lt.text = "화면 비율 / 해상도";
                        aspectDropdown = dd0.GetComponentInChildren<TMP_Dropdown>(true);
                        EnsureValidDropdownTemplate(aspectDropdown, _pretendardFont);
                        EnsureExpandedRowLayout(dd0, 72f);
                    }

                    Transform dd1 = FindDeepChild(videoInnerGroup, "dropdown_labeled (1)");
                    if (dd1 != null)
                    {
                        dd1.gameObject.SetActive(true);
                        Transform lbl = FindDeepChild(dd1, "title");
                        if (lbl != null && lbl.TryGetComponent(out TextMeshProUGUI lt)) lt.text = "화면 모드 (풀스크린 / 창모드)";
                        screenModeDropdown = dd1.GetComponentInChildren<TMP_Dropdown>(true);
                        EnsureValidDropdownTemplate(screenModeDropdown, _pretendardFont);
                        EnsureExpandedRowLayout(dd1, 72f);
                    }

                    // 직계 자식 중 여분 토글만 삭제 (TMP_Dropdown Template 내부 Item Toggle은 절대 건드리지 않음)
                    for (int i = videoInnerGroup.childCount - 1; i >= 0; i--)
                    {
                        Transform child = videoInnerGroup.GetChild(i);
                        if (child.TryGetComponent(out Toggle childToggle) && childToggle != bgSoundToggle)
                        {
                            DestroyImmediate(child.gameObject);
                        }
                    }
                }
            }

            // =================================================================
            // 오디오 패널 정리: 마스터 / BGM / 효과음 슬라이더 3종 바인딩
            // =================================================================
            Slider masterSlider = null, bgmSlider = null, sfxSlider = null;
            TextMeshProUGUI masterValTxt = null, bgmValTxt = null, sfxValTxt = null;

            if (audioPanelTr != null)
            {
                audioPanelTr.gameObject.SetActive(true);
                FixScrollPanelFullWidth(audioPanelTr);

                Transform audioOuterGroup = FindDeepChild(audioPanelTr, "group");
                Transform audioInnerGroup = audioOuterGroup != null ? FindDeepChild(audioOuterGroup, "group (4)") : null;

                if (audioOuterGroup != null)
                {
                    Transform gTitle = FindDeepChild(audioOuterGroup, "title");
                    if (gTitle != null && gTitle.TryGetComponent(out TextMeshProUGUI gt)) gt.text = "오디오 설정";
                }

                if (audioInnerGroup != null)
                {
                    for (int i = audioInnerGroup.childCount - 1; i >= 0; i--)
                    {
                        Transform child = audioInnerGroup.GetChild(i);
                        if (child.GetComponent<Toggle>() != null || child.GetComponentInChildren<TMP_Dropdown>(true) != null)
                        {
                            DestroyImmediate(child.gameObject);
                        }
                    }

                    Slider[] sliders = audioInnerGroup.GetComponentsInChildren<Slider>(true);
                    List<Slider> unassigned = new List<Slider>();

                    for (int i = 0; i < sliders.Length; i++)
                    {
                        Slider s = sliders[i];
                        if (s.gameObject.name != "slider") continue;

                        s.minValue = 0f;
                        s.maxValue = 100f;
                        s.wholeNumbers = true;

                        Transform rowTr = s.transform.parent;
                        EnsureExpandedRowLayout(rowTr, 72f);

                        TextMeshProUGUI lblTmp = FindDeepChild(rowTr, "label")?.GetComponent<TextMeshProUGUI>();
                        string rawLabel = lblTmp != null ? lblTmp.text.ToLowerInvariant() : string.Empty;

                        if (masterSlider == null && (rawLabel.Contains("master") || rawLabel.Contains("마스터")))
                        {
                            masterSlider = s;
                            SetSliderLabels(rowTr, "마스터 볼륨", out masterValTxt);
                        }
                        else if (bgmSlider == null && (rawLabel.Contains("music") || rawLabel.Contains("bgm")))
                        {
                            bgmSlider = s;
                            SetSliderLabels(rowTr, "BGM 볼륨", out bgmValTxt);
                        }
                        else if (sfxSlider == null && (rawLabel.Contains("sound") || rawLabel.Contains("sfx") || rawLabel.Contains("효과음")))
                        {
                            sfxSlider = s;
                            SetSliderLabels(rowTr, "효과음 볼륨", out sfxValTxt);
                        }
                        else
                        {
                            unassigned.Add(s);
                        }
                    }

                    for (int i = 0; i < unassigned.Count; i++)
                    {
                        Slider s = unassigned[i];
                        Transform rowTr = s.transform.parent;
                        if (masterSlider == null)
                        {
                            masterSlider = s;
                            SetSliderLabels(rowTr, "마스터 볼륨", out masterValTxt);
                        }
                        else if (bgmSlider == null)
                        {
                            bgmSlider = s;
                            SetSliderLabels(rowTr, "BGM 볼륨", out bgmValTxt);
                        }
                        else if (sfxSlider == null)
                        {
                            sfxSlider = s;
                            SetSliderLabels(rowTr, "효과음 볼륨", out sfxValTxt);
                        }
                        else
                        {
                            DestroyImmediate(rowTr.gameObject);
                        }
                    }

                    if (masterSlider != null) masterSlider.transform.parent.SetSiblingIndex(0);
                    if (bgmSlider != null) bgmSlider.transform.parent.SetSiblingIndex(1);
                    if (sfxSlider != null) sfxSlider.transform.parent.SetSiblingIndex(2);
                }

                audioPanelTr.gameObject.SetActive(false);
            }

            Toggle videoTab = FindDeepChild(winInstance.transform, "Toggle (11)")?.GetComponent<Toggle>();
            Toggle audioTab = FindDeepChild(winInstance.transform, "Toggle (12)")?.GetComponent<Toggle>();
            if (videoTab != null)
            {
                TextMeshProUGUI tabLbl = videoTab.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tabLbl != null) tabLbl.text = "비디오";
            }
            if (audioTab != null)
            {
                TextMeshProUGUI tabLbl = audioTab.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tabLbl != null) tabLbl.text = "오디오";
            }

            Transform titleBar = FindDeepChild(winInstance.transform, "titlebar");
            if (titleBar != null)
            {
                Transform titleTextTr = FindDeepChild(titleBar, "title");
                if (titleTextTr != null && titleTextTr.TryGetComponent(out TextMeshProUGUI titleTmp))
                    titleTmp.text = "설정";
            }

            Button topCloseBtn = FindDeepChild(winInstance.transform, "btn_close")?.GetComponent<Button>();
            Button resetBtn = null;
            Button bottomCloseBtn = null;
            Transform buttonsRoot = FindDeepChild(winInstance.transform, "buttons");
            if (buttonsRoot != null)
            {
                Button[] bottomButtons = buttonsRoot.GetComponentsInChildren<Button>(true);
                if (bottomButtons.Length >= 1)
                {
                    resetBtn = bottomButtons[0];
                    TextMeshProUGUI t0 = resetBtn.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (t0 != null) t0.text = "기본값 초기화";
                }
                if (bottomButtons.Length >= 2)
                {
                    bottomCloseBtn = bottomButtons[1];
                    TextMeshProUGUI t1 = bottomCloseBtn.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (t1 != null) t1.text = "닫기";
                }
            }

            SerializedObject so = new SerializedObject(view);
            SetEnumRef(so, "<Identifier>k__BackingField", (int)UIId.SettingUI);
            SetEnumRef(so, "<Layer>k__BackingField", (int)UILayer.Window);
            SetEnumRef(so, "_pausePolicy", (int)WindowPausePolicy.PauseTime);
            AssignCanvasGroupRef(so, cg);

            SetObjectRef(so, "_videoTabToggle", videoTab);
            SetObjectRef(so, "_audioTabToggle", audioTab);
            if (videoPanelTr != null) SetObjectRef(so, "_videoPanel", videoPanelTr.gameObject);
            if (audioPanelTr != null) SetObjectRef(so, "_audioPanel", audioPanelTr.gameObject);

            SetObjectRef(so, "_aspectRatioDropdown", aspectDropdown);
            SetObjectRef(so, "_screenModeDropdown", screenModeDropdown);
            SetObjectRef(so, "_backgroundSoundToggle", bgSoundToggle);

            SetObjectRef(so, "_masterVolumeSlider", masterSlider);
            SetObjectRef(so, "_masterValueText", masterValTxt);
            SetObjectRef(so, "_bgmVolumeSlider", bgmSlider);
            SetObjectRef(so, "_bgmValueText", bgmValTxt);
            SetObjectRef(so, "_sfxVolumeSlider", sfxSlider);
            SetObjectRef(so, "_sfxValueText", sfxValTxt);

            SetObjectRef(so, "_topCloseButton", topCloseBtn);
            SetObjectRef(so, "_closeButton", bottomCloseBtn);
            SetObjectRef(so, "_resetDefaultButton", resetBtn);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, destPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _statusLog = $"[성공] {destPath} TMP_Dropdown Template 복구 및 전체 바인딩 완료";
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    /// <summary>
    /// TMP_Dropdown의 Template -> Viewport -> Content -> Item (Toggle) 구조를 검증하고 누락 시 자동 복구
    /// </summary>
    private static void EnsureValidDropdownTemplate(TMP_Dropdown dropdown, TMP_FontAsset fontAsset)
    {
        if (dropdown == null) return;

        if (dropdown.captionText != null)
        {
            dropdown.captionText.font = fontAsset;
            dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        RectTransform templateRt = dropdown.template != null
            ? dropdown.template
            : dropdown.transform.Find("Template") as RectTransform;

        if (templateRt == null)
        {
            GameObject templateGo = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            templateGo.transform.SetParent(dropdown.transform, false);
            templateRt = templateGo.GetComponent<RectTransform>();
            templateGo.GetComponent<Image>().color = new Color(0.11f, 0.09f, 0.07f, 0.98f);
        }

        templateRt.anchorMin = new Vector2(0f, 0f);
        templateRt.anchorMax = new Vector2(1f, 0f);
        templateRt.pivot = new Vector2(0.5f, 1f);
        templateRt.anchoredPosition = Vector2.zero;
        templateRt.sizeDelta = new Vector2(0f, 220f);

        Toggle itemToggle = templateRt.GetComponentInChildren<Toggle>(true);
        if (itemToggle == null || itemToggle.transform == templateRt || !(itemToggle.transform.parent is RectTransform))
        {
            // 기존 깨진 하위 정리 후 표준 TMP_Dropdown Template 계층 구축
            for (int i = templateRt.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(templateRt.GetChild(i).gameObject);
            }

            GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(templateRt, false);
            RectTransform vpRt = viewportGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(4f, 4f);
            vpRt.offsetMax = new Vector2(-4f, -4f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(vpRt, false);
            RectTransform contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 36f);

            ScrollRect scroll = templateRt.GetComponent<ScrollRect>() ?? templateRt.gameObject.AddComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = vpRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject itemGo = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemGo.transform.SetParent(contentRt, false);
            RectTransform itemRt = itemGo.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0f, 0.5f);
            itemRt.anchorMax = new Vector2(1f, 0.5f);
            itemRt.pivot = new Vector2(0.5f, 0.5f);
            itemRt.anchoredPosition = Vector2.zero;
            itemRt.sizeDelta = new Vector2(0f, 36f);

            GameObject bgGo = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(itemRt, false);
            StretchToParent(bgGo.GetComponent<RectTransform>());
            Image bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.18f, 0.15f, 0.11f, 1f);

            GameObject checkGo = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            checkGo.transform.SetParent(itemRt, false);
            RectTransform chkRt = checkGo.GetComponent<RectTransform>();
            chkRt.anchorMin = new Vector2(0f, 0.5f);
            chkRt.anchorMax = new Vector2(0f, 0.5f);
            chkRt.pivot = new Vector2(0.5f, 0.5f);
            chkRt.anchoredPosition = new Vector2(18f, 0f);
            chkRt.sizeDelta = new Vector2(16f, 16f);
            Image chkImg = checkGo.GetComponent<Image>();
            chkImg.color = new Color(0.96f, 0.80f, 0.45f, 1f);

            GameObject labelGo = new GameObject("Item Label", typeof(RectTransform));
            labelGo.transform.SetParent(itemRt, false);
            RectTransform lblRt = labelGo.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(36f, 2f);
            lblRt.offsetMax = new Vector2(-10f, -2f);

            TextMeshProUGUI itemTmp = labelGo.AddComponent<TextMeshProUGUI>();
            itemTmp.font = fontAsset;
            itemTmp.fontSize = 18f;
            itemTmp.alignment = TextAlignmentOptions.MidlineLeft;
            itemTmp.color = new Color(0.95f, 0.90f, 0.82f, 1f);
            itemTmp.text = "Option";

            itemToggle = itemGo.GetComponent<Toggle>();
            itemToggle.targetGraphic = bgImg;
            itemToggle.graphic = chkImg;
            itemToggle.isOn = true;

            dropdown.itemText = itemTmp;
        }
        else
        {
            TextMeshProUGUI existingItemTmp = itemToggle.GetComponentInChildren<TextMeshProUGUI>(true);
            if (existingItemTmp != null)
            {
                existingItemTmp.font = fontAsset;
                existingItemTmp.alignment = TextAlignmentOptions.MidlineLeft;
                dropdown.itemText = existingItemTmp;
            }
        }

        dropdown.template = templateRt;
        templateRt.gameObject.SetActive(false);
    }

    // --- SettingUI 가로폭 확장 및 불필요 스크롤바 정리 헬퍼 ---
    private static void DestroyChildIfExists(Transform root, string childName)
    {
        Transform target = FindDeepChild(root, childName);
        if (target != null)
        {
            DestroyImmediate(target.gameObject);
        }
    }

    private static void FixScrollPanelFullWidth(Transform panelTr)
    {
        if (panelTr == null) return;

        // 항목이 3개뿐이므로 불필요한 세로 스크롤바 제거 후 Viewport를 패널 전체로 확장
        Transform scrollbar = FindDeepChild(panelTr, "Scrollbar Vertical");
        if (scrollbar != null) DestroyImmediate(scrollbar.gameObject);

        if (panelTr.TryGetComponent(out ScrollRect sr))
        {
            sr.verticalScrollbar = null;
            sr.horizontal = false;
            sr.vertical = false;
        }

        Transform viewport = FindDeepChild(panelTr, "Viewport");
        if (viewport != null && viewport.TryGetComponent(out RectTransform vpRt))
        {
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.pivot = new Vector2(0f, 1f);
            vpRt.offsetMin = new Vector2(8f, 8f);
            vpRt.offsetMax = new Vector2(-8f, -8f);
        }

        Transform content = FindDeepChild(panelTr, "ContentVideo");
        if (content != null && content.TryGetComponent(out RectTransform contentRt))
        {
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = new Vector2(0f, contentRt.offsetMin.y);
            contentRt.offsetMax = new Vector2(0f, 0f);
        }

        foreach (VerticalLayoutGroup vlg in panelTr.GetComponentsInChildren<VerticalLayoutGroup>(true))
        {
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
        }
    }

    private static void EnsureExpandedRowLayout(Transform rowTr, float preferredHeight)
    {
        if (rowTr == null) return;
        LayoutElement le = rowTr.GetComponent<LayoutElement>() ?? rowTr.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.flexibleWidth = 1f;
    }

    // =========================================================================
    // 2. SystemMenuUI.prefab 비율 교정 (풀스트레치 루트 + 중앙 325x368 패널)
    // =========================================================================
    private void BuildSystemMenuPrefab()
    {
        if (_systemMenuSourcePrefab == null) return;

        EnsureOutputDirectory();
        string destPath = $"{OutputFolder}/SystemMenuUI.prefab";

        GameObject root = new GameObject("SystemMenuUI", typeof(RectTransform));
        StretchToParent(root.GetComponent<RectTransform>());

        try
        {
            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            SystemMenuView view = root.AddComponent<SystemMenuView>();

            // 반투명 딤 배경
            Image dimmer = root.AddComponent<Image>();
            dimmer.color = new Color(0.02f, 0.02f, 0.02f, 0.55f);

            // 기존 SystemMenuUI를 중앙 325x368 자식 패널로 언팩 배치
            GameObject panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(_systemMenuSourcePrefab, root.transform);
            if (PrefabUtility.IsPartOfPrefabInstance(panelInstance))
            {
                PrefabUtility.UnpackPrefabInstance(panelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            // 이미 루트가 분리된 상태라면 내부 MenuPanel 추출
            Transform existingPanel = FindDeepChild(panelInstance.transform, "MenuPanel");
            if (existingPanel != null && existingPanel.gameObject != panelInstance)
            {
                existingPanel.SetParent(root.transform, false);
                DestroyImmediate(panelInstance);
                panelInstance = existingPanel.gameObject;
            }
            panelInstance.name = "MenuPanel";

            // 루트에 붙어 있던 구형 SystemMenuView 컴포넌트가 있다면 제거
            if (panelInstance.TryGetComponent(out SystemMenuView oldView))
            {
                DestroyImmediate(oldView, true);
            }

            RectTransform panelRt = panelInstance.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(360f, 390f);
            panelRt.localScale = Vector3.one;

            CleanupBrokenComponents(root);
            ConvertLegacyTextAndDropdownToTMP(panelInstance, _pretendardFont);

            foreach (Button b in panelInstance.GetComponentsInChildren<Button>(true))
            {
                ClearPersistentButtonEvents(b);
            }

            Button resumeBtn = FindDeepChild(panelInstance.transform, "Btn_Resume")?.GetComponent<Button>();
            Button settingBtn = FindDeepChild(panelInstance.transform, "Btn_Settings")?.GetComponent<Button>();
            Button quitBtn = FindDeepChild(panelInstance.transform, "Btn_Quit")?.GetComponent<Button>();

            SerializedObject so = new SerializedObject(view);
            SetEnumRef(so, "<Identifier>k__BackingField", (int)UIId.SystemMenuUI);
            SetEnumRef(so, "<Layer>k__BackingField", (int)UILayer.Window);
            SetEnumRef(so, "_pausePolicy", (int)WindowPausePolicy.PauseTime);
            AssignCanvasGroupRef(so, cg);

            SetObjectRef(so, "_resumeButton", resumeBtn);
            SetObjectRef(so, "_settingButton", settingBtn);
            SetObjectRef(so, "_lobbyButton", quitBtn);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, destPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _statusLog = $"[성공] {destPath} 중앙 360x390 패널 분리 및 TMP 변환 완료";
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    // =========================================================================
    // 공통 유틸리티
    // =========================================================================
    private static void ClearPersistentSliderEvents(Slider slider)
    {
        if (slider == null) return;
        for (int i = slider.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(slider.onValueChanged, i);
        }
    }

    private static void ClearPersistentButtonEvents(Button btn)
    {
        if (btn == null) return;
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(btn.onClick, i);
        }
    }

    private static void ClearPersistentToggleEvents(Toggle toggle)
    {
        if (toggle == null) return;
        for (int i = toggle.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(toggle.onValueChanged, i);
        }
    }

    private static void ConvertLegacyTextAndDropdownToTMP(GameObject root, TMP_FontAsset fontAsset)
    {
        Dropdown[] legacyDropdowns = root.GetComponentsInChildren<Dropdown>(true);
        List<DropdownSnapshot> ddSnapshots = new List<DropdownSnapshot>(legacyDropdowns.Length);
        foreach (Dropdown dd in legacyDropdowns)
        {
            ddSnapshots.Add(new DropdownSnapshot
            {
                TargetGo = dd.gameObject,
                TargetGraphic = dd.targetGraphic,
                Template = dd.template,
                CaptionTextGo = dd.captionText != null ? dd.captionText.gameObject : null,
                ItemTextGo = dd.itemText != null ? dd.itemText.gameObject : null
            });
            DestroyImmediate(dd, true);
        }

        foreach (Text legacy in root.GetComponentsInChildren<Text>(true))
        {
            GameObject go = legacy.gameObject;
            string content = legacy.text;
            int fontSize = legacy.fontSize;
            Color color = legacy.color;
            TextAnchor anchor = legacy.alignment;
            FontStyle style = legacy.fontStyle;
            bool raycast = legacy.raycastTarget;

            DestroyImmediate(legacy, true);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = fontAsset;
            tmp.text = content;
            tmp.fontSize = fontSize > 0 ? fontSize : 18f;
            tmp.color = color;
            tmp.alignment = anchor switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
                TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center
            };
            tmp.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.raycastTarget = raycast;
        }

        foreach (DropdownSnapshot snap in ddSnapshots)
        {
            TMP_Dropdown tmpDd = snap.TargetGo.AddComponent<TMP_Dropdown>();
            tmpDd.targetGraphic = snap.TargetGraphic;
            tmpDd.template = snap.Template;
            if (snap.CaptionTextGo != null) tmpDd.captionText = snap.CaptionTextGo.GetComponent<TextMeshProUGUI>();
            if (snap.ItemTextGo != null) tmpDd.itemText = snap.ItemTextGo.GetComponent<TextMeshProUGUI>();
        }
    }

    private struct DropdownSnapshot
    {
        public GameObject TargetGo;
        public Graphic TargetGraphic;
        public RectTransform Template;
        public GameObject CaptionTextGo;
        public GameObject ItemTextGo;
    }

    private static void SetSliderLabels(Transform container, string titleText, out TextMeshProUGUI valueTmp)
    {
        valueTmp = null;
        if (container == null) return;

        Transform lblTr = FindDeepChild(container, "label");
        if (lblTr != null && lblTr.TryGetComponent(out TextMeshProUGUI lblTmp))
            lblTmp.text = titleText;

        Transform valTr = FindDeepChild(container, "value");
        if (valTr != null && valTr.TryGetComponent(out TextMeshProUGUI vTmp))
            valueTmp = vTmp;
    }

    private static void CleanupBrokenComponents(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach (EventTrigger et in t.GetComponents<EventTrigger>())
            {
                DestroyImmediate(et, true);
            }
        }
    }

    private static void StretchToParent(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static void SetObjectRef(SerializedObject so, string propName, UnityEngine.Object value)
    {
        SerializedProperty prop = so?.FindProperty(propName);
        if (prop != null) prop.objectReferenceValue = value;
    }

    private static void SetEnumRef(SerializedObject so, string propName, int value)
    {
        SerializedProperty prop = so?.FindProperty(propName);
        if (prop != null) prop.intValue = value;
    }

    private static void AssignCanvasGroupRef(SerializedObject so, CanvasGroup cg)
    {
        SerializedProperty prop = so?.FindProperty("_canvasGroup") ?? so?.FindProperty("canvasGroup");
        if (prop != null) prop.objectReferenceValue = cg;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif