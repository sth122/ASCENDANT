// [QuickSlotUIPrefabBuilder.cs - 좌하단 다이아몬드 십자 앵커 및 단축키 인디케이터 교정]
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class QuickSlotUIPrefabBuilder
{
    private const string PrefabSavePath = "Assets/07.Prefabs/UI/QuickSlotUI.prefab";

    [MenuItem("ASCENDANT/UI/Build QuickSlot UI Prefab")]
    public static void BuildPrefab()
    {
        // 1. 루트 오브젝트 생성 (화면 좌하단 고정: image_7e720c.jpg 중앙 뭉침 완전 해결)
        GameObject rootGo = new GameObject("QuickSlotUI", typeof(RectTransform), typeof(CanvasGroup), typeof(QuickSlotView));
        RectTransform rootRect = rootGo.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(160f, 160f);
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot = new Vector2(0f, 0f);
        rootRect.anchoredPosition = new Vector2(120f, 120f); // 좌하단 정확한 오프셋

        // 2. 다이아몬드(◇) 십자 좌표 정의
        Vector2[] diamondPositions = new Vector2[]
        {
            new Vector2(0f, 52f),   // 슬롯 0: 상단 (Key 1)
            new Vector2(-52f, 0f),  // 슬롯 1: 좌측 (Key 2)
            new Vector2(52f, 0f),   // 슬롯 2: 우측 (Key 3)
            new Vector2(0f, -52f)   // 슬롯 3: 하단 (Key 4)
        };

        string[] keyLabels = { "1", "2", "3", "4" };
        QuickSlotItemUI[] slotItemUIs = new QuickSlotItemUI[4];

        for (int i = 0; i < 4; i++)
        {
            slotItemUIs[i] = CreateDiamondSlotUI(rootGo.transform, i, diamondPositions[i], keyLabels[i]);
        }

        // 3. SerializedObject로 QuickSlotView에 슬롯 배열 바인딩
        QuickSlotView view = rootGo.GetComponent<QuickSlotView>();
        SerializedObject so = new SerializedObject(view);
        SerializedProperty slotsProp = so.FindProperty("_slotUIs");
        slotsProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotItemUIs[i];
        }
        so.ApplyModifiedProperties();

        // 4. 프리팹 에셋 저장
        EnsureDirectory("Assets/07.Prefabs/UI");
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabSavePath);
        Object.DestroyImmediate(rootGo);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = savedPrefab;

        Debug.Log($"[QuickSlotUI 다이아몬드 프리팹 생성 완료] 경로: {PrefabSavePath}");
    }

    private static QuickSlotItemUI CreateDiamondSlotUI(Transform parent, int index, Vector2 anchoredPos, string keyLabel)
    {
        GameObject slotGo = new GameObject($"DiamondSlot_{index}", typeof(RectTransform), typeof(Image), typeof(QuickSlotItemUI));
        slotGo.transform.SetParent(parent, false);

        RectTransform slotRect = slotGo.GetComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(50f, 50f);
        slotRect.anchoredPosition = anchoredPos;

        Image bgImg = slotGo.GetComponent<Image>();
        bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.88f);

        // 아이콘 (40x40)
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(slotGo.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(40f, 40f);
        Image iconImg = iconGo.GetComponent<Image>();
        iconImg.color = Color.white;
        iconGo.SetActive(false);

        // 쿨타임 Radial 오버레이
        GameObject cdGo = new GameObject("CooldownRadial", typeof(RectTransform), typeof(Image));
        cdGo.transform.SetParent(slotGo.transform, false);
        SetFullStretch(cdGo.GetComponent<RectTransform>());
        Image cdImg = cdGo.GetComponent<Image>();
        cdImg.color = new Color(0f, 0f, 0f, 0.75f);
        cdImg.type = Image.Type.Filled;
        cdImg.fillMethod = Image.FillMethod.Radial360;
        cdImg.fillOrigin = (int)Image.Origin360.Top;
        cdImg.fillAmount = 0f;

        // 수량 텍스트
        GameObject countGo = new GameObject("AmountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        countGo.transform.SetParent(slotGo.transform, false);
        RectTransform countRect = countGo.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(1f, 0f);
        countRect.anchorMax = new Vector2(1f, 0f);
        countRect.pivot = new Vector2(1f, 0f);
        countRect.sizeDelta = new Vector2(28f, 18f);
        TextMeshProUGUI countTmp = countGo.GetComponent<TextMeshProUGUI>();
        countTmp.fontSize = 13;
        countTmp.fontStyle = FontStyles.Bold;
        countTmp.alignment = TextAlignmentOptions.BottomRight;
        countGo.SetActive(false);

        // 단축키 라벨 ("1", "2", "3", "4")
        GameObject keyGo = new GameObject("HotkeyLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        keyGo.transform.SetParent(slotGo.transform, false);
        RectTransform keyRect = keyGo.GetComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0.5f, 0f);
        keyRect.anchorMax = new Vector2(0.5f, 0f);
        keyRect.pivot = new Vector2(0.5f, 1f);
        keyRect.anchoredPosition = new Vector2(0f, -2f);
        keyRect.sizeDelta = new Vector2(28f, 16f);
        TextMeshProUGUI keyTmp = keyGo.GetComponent<TextMeshProUGUI>();
        keyTmp.text = keyLabel;
        keyTmp.fontSize = 12;
        keyTmp.alignment = TextAlignmentOptions.Center;
        keyTmp.color = new Color(0.85f, 0.85f, 0.85f, 0.95f);

        // 선택 하이라이트 아웃라인
        GameObject outlineGo = new GameObject("SelectionOutline", typeof(RectTransform), typeof(Image));
        outlineGo.transform.SetParent(slotGo.transform, false);
        SetFullStretch(outlineGo.GetComponent<RectTransform>());
        Image outImg = outlineGo.GetComponent<Image>();
        outImg.color = new Color(1f, 0.82f, 0.2f, 0.9f);
        outlineGo.SetActive(false);

        QuickSlotItemUI itemUI = slotGo.GetComponent<QuickSlotItemUI>();
        SerializedObject so = new SerializedObject(itemUI);
        so.FindProperty("_iconImage").objectReferenceValue = iconImg;
        so.FindProperty("_amountText").objectReferenceValue = countTmp;
        so.FindProperty("_cooldownRadialImage").objectReferenceValue = cdImg;
        so.FindProperty("_selectionOutline").objectReferenceValue = outlineGo;
        so.ApplyModifiedProperties();

        return itemUI;
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