#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PlayerHUDPrefabCustomizer
{
    [MenuItem("Tools/Overhaul PlayerHUD Prefab", false, 15)]
    public static void OverhaulPrefab()
    {
        string[] guids = AssetDatabase.FindAssets("PlayerHUD t:Prefab");
        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "PlayerHUD 프리팹을 찾을 수 없습니다.", "확인");
            return;
        }

        string prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "프리팹 로드에 실패했습니다.", "확인");
            return;
        }

        try
        {
            // 1. Panel 높이 확장 (85px)
            Transform panelTr = prefabRoot.transform.Find("Panel");
            if (panelTr is RectTransform panelRect)
            {
                panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, 85f);
            }

            // 2. portrait 아이콘 셀 크기 및 패딩 정돈
            Transform portraitTr = prefabRoot.transform.Find("Panel/content/portrait");
            if (portraitTr != null)
            {
                if (portraitTr.TryGetComponent(out LayoutElement le))
                {
                    le.preferredWidth = 80f;
                    le.preferredHeight = 80f;
                }

                Transform iconTr = portraitTr.Find("icon");
                if (iconTr is RectTransform iconRect)
                {
                    iconRect.anchorMin = Vector2.zero;
                    iconRect.anchorMax = Vector2.one;
                    iconRect.offsetMin = new Vector2(4f, 4f);
                    iconRect.offsetMax = new Vector2(-4f, -4f);
                }
            }

            // 3. bars 컨테이너 설정 (가로폭 독립 제어 활성화)
            Transform barsTr = prefabRoot.transform.Find("Panel/content/content_padding/bars");
            if (barsTr != null && barsTr.TryGetComponent(out VerticalLayoutGroup vlg))
            {
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = false;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = false;
                vlg.childForceExpandHeight = false;
                vlg.spacing = 1f;
            }

            // 4. [마스크 적용] 각 바마다 내부에 InnerTrack_Mask(RectMask2D)를 생성하고 슬라이더를 마스킹 구역으로 편입
            RectTransform hpBar = SetupMaskedBarAndSliders(barsTr, "hp_bar", 480f, 22f, 4f, 3f);
            RectTransform stamBar = SetupMaskedBarAndSliders(barsTr, "stamina_bar", 320f, 18f, 4f, 3f);
            RectTransform poiseBar = SetupMaskedBarAndSliders(barsTr, "poise_bar", 220f, 14f, 4f, 2.5f);

            // 5. PlayerHUDView 직렬화 필드 바인딩
            if (prefabRoot.TryGetComponent(out PlayerHUDView hudView))
            {
                SerializedObject so = new SerializedObject(hudView);
                so.FindProperty("_hpBarRect").objectReferenceValue = hpBar;
                so.FindProperty("_staminaBarRect").objectReferenceValue = stamBar;
                so.FindProperty("_poiseBarRect").objectReferenceValue = poiseBar;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("PlayerHUD", "프레임 틀 내부 마스크(RectMask2D) 기법 적용 및 슬라이더 매립 보정이 완료되었습니다.", "확인");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static RectTransform SetupMaskedBarAndSliders(Transform barsTr, string barName, float defaultWidth, float height, float horizontalInset, float verticalInset)
    {
        if (barsTr == null) return null;
        Transform barTr = barsTr.Find(barName);
        if (barTr == null) return null;

        RectTransform barRect = barTr as RectTransform;
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.sizeDelta = new Vector2(defaultWidth, height);

        if (barTr.TryGetComponent(out LayoutElement le))
        {
            le.ignoreLayout = false;
            le.preferredWidth = -1;
            le.preferredHeight = height;
        }

        // 1. Background (외곽 금속 프레임 틀) -> 전체 영역 꽉 채움
        Transform bgTr = barTr.Find("Background");
        if (bgTr is RectTransform bgRect)
        {
            SetFullStretch(bgRect, 0f, 0f);
            if (bgTr.TryGetComponent(out Image bgImg))
            {
                bgImg.color = Color.white;
                bgImg.type = Image.Type.Sliced;
            }
        }

        // 2. [마스크 컨테이너] InnerTrack_Mask 생성 및 RectMask2D 부착
        Transform maskTr = barTr.Find("InnerTrack_Mask");
        if (maskTr == null)
        {
            GameObject maskGo = new GameObject("InnerTrack_Mask", typeof(RectTransform), typeof(RectMask2D));
            maskTr = maskGo.transform;
            maskTr.SetParent(barTr, false);
        }
        RectTransform maskRect = maskTr as RectTransform;
        // 프레임 틀의 상하좌우 베벨 두께만큼 안쪽으로 인셋하여 마스크 영역 지정
        SetFullStretch(maskRect, horizontalInset, verticalInset);

        // 기존에 바 바로 아래에 있던 슬라이더들을 InnerTrack_Mask 자식으로 이동
        for (int i = barTr.childCount - 1; i >= 0; i--)
        {
            Transform child = barTr.GetChild(i);
            if (child.name.Contains("Slider") && child != maskTr)
            {
                child.SetParent(maskTr, false);
            }
        }

        // 3. 슬라이더들 내부 셋팅 (마스크 안에서 꽉 차게 렌더링)
        for (int i = 0; i < maskTr.childCount; i++)
        {
            Transform child = maskTr.GetChild(i);
            if (!child.name.Contains("Slider")) continue;

            RectTransform sliderRect = child as RectTransform;
            SetFullStretch(sliderRect, 0f, 0f);

            Transform fillArea = child.Find("Fill Area (1)") ?? child.Find("Fill Area");
            if (fillArea is RectTransform fillAreaRect)
            {
                SetFullStretch(fillAreaRect, 0f, 0f);
                Transform fill = fillArea.Find("Fill");
                if (fill is RectTransform fillRect)
                {
                    fillRect.anchorMin = new Vector2(0f, 0f);
                    fillRect.anchorMax = new Vector2(0f, 1f);
                    fillRect.offsetMin = Vector2.zero;
                    fillRect.offsetMax = Vector2.zero;
                    fillRect.pivot = new Vector2(0f, 0.5f);

                    if (fill.TryGetComponent(out Image fillImg))
                    {
                        fillImg.type = Image.Type.Sliced;
                    }
                }
            }
        }

        return barRect;
    }

    private static void SetFullStretch(RectTransform rt, float horizontalInset, float verticalInset)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(horizontalInset, verticalInset);
        rt.offsetMax = new Vector2(-horizontalInset, -verticalInset);
    }
}
#endif