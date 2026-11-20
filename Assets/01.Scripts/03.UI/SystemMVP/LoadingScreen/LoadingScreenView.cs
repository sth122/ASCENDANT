/// <summary>
/// 씬 전환 로딩 화면 뷰 (LoadingUI 프리팹 대응)
/// </summary>
[UnityEngine.RequireComponent(typeof(UnityEngine.CanvasGroup))]
public class LoadingScreenView : UIBase
{
    [UnityEngine.Header("Loading UI Elements")]
    [UnityEngine.Tooltip("bottom -> slider 오브젝트의 Slider 컴포넌트")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _progressBar;

    [UnityEngine.Tooltip("Text 오브젝트의 UnityEngine.UI.Text 컴포넌트")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Text _loadingText;

    public override void InitializeView()
    {
        base.InitializeView();
        Identifier = UIId.LoadingScreenUI;
        Layer = UILayer.System;
        IsGlobalUI = true;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = false;
        }
    }

    /// <summary>
    /// 진행도(0.0 ~ 1.0)를 받아 슬라이더의 MaxValue에 맞춰 반영
    /// </summary>
    public void UpdateProgress(float normalizedProgress, string description = null)
    {
        if (_progressBar != null)
        {
            float max = _progressBar.maxValue > 0f ? _progressBar.maxValue : 100f;
            _progressBar.value = UnityEngine.Mathf.Clamp01(normalizedProgress) * max;
        }

        if (_loadingText != null && !string.IsNullOrEmpty(description))
        {
            _loadingText.text = description;
        }
    }
}