#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static partial class MockAssetGenerator
{
    [MenuItem("Tools/어센던트/Create GameOverUI Prefab")]
    public static void CreateGameOverUIPrefab()
    {
        // 1. 루트 캔버스 오브젝트 생성
        GameObject root = new GameObject("GameOverUI", typeof(RectTransform), typeof(CanvasGroup), typeof(GameOverView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(1920, 1080);

        // 2. 반투명 블랙 암전 배경 (Dimmer)
        GameObject bgGo = new GameObject("BackgroundDimmer", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(root.transform, false);
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgGo.GetComponent<Image>();
        bgImg.color = new Color(0.02f, 0.02f, 0.02f, 0.85f);
        bgImg.raycastTarget = true;

        // 3. YOU DIED 텍스트
        GameObject textGo = new GameObject("YouDiedText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(root.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchoredPosition = new Vector2(0, 80);
        textRect.sizeDelta = new Vector2(1000, 200);
        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "YOU DIED";
        tmp.fontSize = 84;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.85f, 0.1f, 0.1f, 1f); // 핏빛 다크레드
        tmp.fontStyle = FontStyles.Bold;

        // 4. 버튼 컨테이너 그룹
        GameObject btnGroupGo = new GameObject("ButtonGroup", typeof(RectTransform), typeof(CanvasGroup), typeof(VerticalLayoutGroup));
        btnGroupGo.transform.SetParent(root.transform, false);
        RectTransform btnGroupRect = btnGroupGo.GetComponent<RectTransform>();
        btnGroupRect.anchoredPosition = new Vector2(0, -140);
        btnGroupRect.sizeDelta = new Vector2(360, 160);

        VerticalLayoutGroup layout = btnGroupGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 16;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        // 5. 버튼 생성 헬퍼
        Button respawnBtn = CreateButton(btnGroupGo.transform, "RespawnButton", "마지막 화톳불에서 재시작");
        Button titleBtn = CreateButton(btnGroupGo.transform, "TitleButton", "타이틀 화면으로");

        // 6. View 직렬화 필드 연결
        SerializedObject so = new SerializedObject(root.GetComponent<GameOverView>());
        so.FindProperty("_youDiedText").objectReferenceValue = tmp;
        so.FindProperty("_buttonGroup").objectReferenceValue = btnGroupGo.GetComponent<CanvasGroup>();
        so.FindProperty("_respawnButton").objectReferenceValue = respawnBtn;
        so.FindProperty("_titleButton").objectReferenceValue = titleBtn;
        so.FindProperty("_pauseTimeWhenOpened").boolValue = false;
        so.ApplyModifiedProperties();

        // 7. 프리팹 에셋 저장 및 Addressables 등록
        string folder = "Assets/02.Prefabs/UI";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/02.Prefabs", "UI");
        string path = $"{folder}/GameOverUI.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        Image img = btnGo.GetComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btnGo.GetComponent<Button>();
    }
}
#endif