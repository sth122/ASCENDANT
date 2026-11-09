using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 씬 전환 시 화면 Fade Out / Fade In 최상위 시스템 UI 뷰
/// </summary>
public class FadeCurtainView : UIBase
{
    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.System;
        IsGlobalUI = true;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 화면을 검은색으로 Fade Out
    /// </summary>
    public async UniTask FadeOutAsync(float duration = 0.4f, System.Threading.CancellationToken ct = default)
    {
        CreateActionToken(ct);
        gameObject.SetActive(true);

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = true;
            await FadeCanvasGroupAsync(1f, duration, _fadeCts.Token);
        }
    }

    /// <summary>
    /// 검은 화면을 밝은 화면으로 Fade In
    /// </summary>
    public async UniTask FadeInAsync(float duration = 0.4f, System.Threading.CancellationToken ct = default)
    {
        CreateActionToken(ct);

        if (_canvasGroup != null)
        {
            await FadeCanvasGroupAsync(0f, duration, _fadeCts.Token);
            _canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }
}