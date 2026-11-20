using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class EndingCreditsPresenter : UIPresenterBase<EndingCreditsView, EndingCreditsModel>
{
    private CancellationTokenSource _creditsCts;
    private bool _isFinishing = false;

    protected override void OnBind()
    {
        OnBindHandler();

        EndingCreditsModel activeModel = Model ?? EndingCreditsModel.CreateDefault();
        View?.SetupCreditsContent(activeModel);

        InputSystemHandler.SetMode(InputMode.RealtimeUI);

        AsyncUtil.CancelAndDispose(ref _creditsCts);
        _creditsCts = new CancellationTokenSource();
        RunCreditsSequenceAsync(activeModel, _creditsCts.Token).Forget();
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
        AsyncUtil.CancelAndDispose(ref _creditsCts);
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();
        if (View != null && View.SkipButton != null)
        {
            View.SkipButton.onClick.AddListener(HandleSkipClicked);
        }
    }

    protected override void OnUnbindHandler()
    {
        if (View != null && View.SkipButton != null)
        {
            View.SkipButton.onClick.RemoveListener(HandleSkipClicked);
        }
    }

    private async UniTaskVoid RunCreditsSequenceAsync(EndingCreditsModel model, CancellationToken ct)
    {
        try
        {
            if (View != null)
            {
                await View.FadeCanvasAsync(1.0f, 1.5f, ct);
            }

            while (!ct.IsCancellationRequested && View != null)
            {
                float speed = model.BaseScrollSpeed;
                if (IsFastForwardPressed())
                {
                    speed *= model.FastForwardMultiplier;
                }

                bool isCompleted = View.TickScroll(speed);
                if (isCompleted)
                {
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // 마지막 문구 여운 대기
            await UniTask.Delay(TimeSpan.FromSeconds(1.5f), ignoreTimeScale: true, cancellationToken: ct);
            await FinishCreditsAndReturnToTitleAsync();
        }
        catch (OperationCanceledException) { }
    }

    private bool IsFastForwardPressed()
    {
        bool keyHold = Keyboard.current != null &&
                       (Keyboard.current.spaceKey.isPressed || Keyboard.current.enterKey.isPressed);
        bool mouseHold = Mouse.current != null && Mouse.current.leftButton.isPressed;
        return keyHold || mouseHold;
    }

    private void HandleSkipClicked()
    {
        if (_isFinishing) return;
        AsyncUtil.CancelAndDispose(ref _creditsCts);
        FinishCreditsAndReturnToTitleAsync().Forget();
    }

    private async UniTask FinishCreditsAndReturnToTitleAsync()
    {
        if (_isFinishing) return;
        _isFinishing = true;

        if (SessionDataManager.Instance != null)
        {
            SessionDataManager.Instance.ClearSession();
        }

        if (SceneLoadManager.Instance != null)
        {
            await SceneLoadManager.Instance.LoadSceneFlowAsync(SceneId.TitleScene);
        }
    }
}