using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndingCreditsView : UIWindowView
{
    [Header("Fade & Viewport")]
    [SerializeField] private CanvasGroup _rootCanvasGroup;
    [SerializeField] private RectTransform _viewportRect;
    [SerializeField] private RectTransform _contentRect;

    [Header("Text Elements")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _creditsBodyText;
    [SerializeField] private TextMeshProUGUI _endingMessageText;

    [Header("Controls")]
    [SerializeField] private Button _skipButton;

    public Button SkipButton => _skipButton;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.Window;
        _pausePolicy = WindowPausePolicy.RealtimeCombat;

        if (_rootCanvasGroup != null)
        {
            _rootCanvasGroup.alpha = 0f;
        }
    }

    public void SetupCreditsContent(EndingCreditsModel model)
    {
        if (model == null) return;

        if (_titleText != null)
        {
            _titleText.text = model.GameTitle;
        }

        if (_creditsBodyText != null)
        {
            _creditsBodyText.overflowMode = TextOverflowModes.Overflow;
            _creditsBodyText.text = BuildFormattedCreditsText(model);
        }

        if (_endingMessageText != null)
        {
            _endingMessageText.text = model.Subtitle;
        }

        if (_contentRect != null && _viewportRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
            float startY = -_viewportRect.rect.height * 0.5f;
            _contentRect.anchoredPosition = new Vector2(0f, startY);
        }
    }

    private string BuildFormattedCreditsText(EndingCreditsModel model)
    {
        StringBuilder sb = new StringBuilder(2048);

        // 1. 개발진 크레딧 섹션
        for (int i = 0; i < model.Entries.Count; i++)
        {
            CreditEntry entry = model.Entries[i];
            sb.AppendLine($"<size=72%><color=#A89F91>{entry.Role}</color></size>");
            sb.AppendLine($"<b>{entry.Name}</b>");
            sb.AppendLine();
        }

        // 2. 에셋 저작권 카테고리 섹션
        if (model.AssetCategories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<size=95%><b><color=#D9B86C>— ASSET CREDITS —</color></b></size>");
            sb.AppendLine();

            for (int c = 0; c < model.AssetCategories.Count; c++)
            {
                AssetCreditCategory category = model.AssetCategories[c];
                if (category == null || category.Items.Count == 0) continue;

                sb.AppendLine($"<size=85%><b><color=#D9B86C>[ {category.CategoryTitle} ]</color></b></size>");
                sb.AppendLine();

                for (int j = 0; j < category.Items.Count; j++)
                {
                    AssetCreditItem item = category.Items[j];

                    if (!string.IsNullOrEmpty(item.ProviderNote))
                    {
                        sb.AppendLine($"<size=76%><b>{item.AssetTitle}</b> <color=#C8B894>({item.ProviderNote})</color></size>");
                    }
                    else
                    {
                        sb.AppendLine($"<size=76%><b>{item.AssetTitle}</b></size>");
                    }

                    if (!string.IsNullOrEmpty(item.SourceUrl))
                    {
                        sb.AppendLine($"<size=56%><color=#9E988E>{item.SourceUrl}</color></size>");
                    }

                    sb.AppendLine();
                }

                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async UniTask FadeCanvasAsync(float targetAlpha, float duration, CancellationToken ct)
    {
        if (_rootCanvasGroup == null) return;

        float startAlpha = _rootCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            ct.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            _rootCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        _rootCanvasGroup.alpha = targetAlpha;
    }

    /// <summary>
    /// 크레딧을 위로 스크롤하며, 모든 텍스트가 화면 상단을 통과하면 true를 반환
    /// </summary>
    public bool TickScroll(float speed)
    {
        if (_contentRect == null || _viewportRect == null) return true;

        Vector2 pos = _contentRect.anchoredPosition;
        pos.y += speed * Time.unscaledDeltaTime;
        _contentRect.anchoredPosition = pos;

        float endY = _contentRect.rect.height + (_viewportRect.rect.height * 0.5f);
        return pos.y >= endY;
    }
}