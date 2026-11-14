using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 게임 저장 완료 시 우측 하단에 '게임 저장' 문구를 3초간 노출 후 페이드 아웃하는 시스템 토스트 뷰
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class SaveToastView : UIBase
{
    [SerializeField] private TMPro.TextMeshProUGUI _messageText;
    [SerializeField] private float _completedDisplayDuration = 3.0f;

    private CancellationTokenSource _toastCts;

    public override void InitializeView()
    {
        base.InitializeView();
        Identifier = UIId.SaveToastUI;
        Layer = UILayer.System;
        IsGlobalUI = true;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
    }

    /// <summary>
    /// 1단계: 저장 진행 중 표시 (완료 이벤트가 올 때까지 유지)
    /// </summary>
    public void ShowSavingState(string message = "게임 저장 중..")
    {
        AsyncUtil.CancelAndDispose(ref _toastCts);
        _toastCts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);

        if (_messageText != null) _messageText.text = message;

        gameObject.SetActive(true);
        IsVisible = true;
        FadeInOnlyAsync(_toastCts.Token).Forget();
    }

    /// <summary>
    /// 2단계: 저장 완료 문구로 변경 후 3초 대기 뒤 페이드 아웃
    /// </summary>
    public void ShowSavedAndHideAfterDelay(string message = "게임 저장 완료")
    {
        AsyncUtil.CancelAndDispose(ref _toastCts);
        _toastCts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);

        if (_messageText != null) _messageText.text = message;

        gameObject.SetActive(true);
        IsVisible = true;
        RunCompletedRoutineAsync(_toastCts.Token).Forget();
    }

    private async UniTaskVoid FadeInOnlyAsync(CancellationToken ct)
    {
        if (_canvasGroup != null && _canvasGroup.alpha < 1f)
        {
            await FadeCanvasGroupAsync(1f, 0.15f, ct);
        }
    }

    private async UniTaskVoid RunCompletedRoutineAsync(CancellationToken ct)
    {
        if (_canvasGroup != null && _canvasGroup.alpha < 1f)
        {
            await FadeCanvasGroupAsync(1f, 0.15f, ct);
        }

        bool canceled = await UniTask.Delay(
            System.TimeSpan.FromSeconds(_completedDisplayDuration),
            ignoreTimeScale: true,
            cancellationToken: ct).SuppressCancellationThrow();
        if (canceled) return;

        if (_canvasGroup != null)
        {
            await FadeCanvasGroupAsync(0f, 0.4f, ct);
        }

        IsVisible = false;
        gameObject.SetActive(false);
    }

    protected override void OnDestroy()
    {
        AsyncUtil.CancelAndDispose(ref _toastCts);
        base.OnDestroy();
    }
}