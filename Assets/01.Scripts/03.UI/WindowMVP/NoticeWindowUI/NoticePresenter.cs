/// <summary>
/// NoticePopupView의 이미지 페이지 이전/다음 전환 및 닫기를 처리하는 프리젠터
/// </summary>
public class NoticePresenter : UIPresenterBase<NoticeWindowView, NoticeModel>
{
    protected override void OnBind()
    {
        if (View == null) return;

        if (Model != null)
        {
            View.RenderPage(Model);
        }

        OnBindHandler();
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();
        if (View == null) return;

        if (View.PrevButton != null) View.PrevButton.onClick.AddListener(HandlePrevClicked);
        if (View.NextButton != null) View.NextButton.onClick.AddListener(HandleNextClicked);
        if (View.ConfirmButton != null) View.ConfirmButton.onClick.AddListener(HandleConfirmClicked);
        if (View.TopCloseButton != null) View.TopCloseButton.onClick.AddListener(HandleConfirmClicked);
    }

    protected override void OnUnbindHandler()
    {
        if (View == null) return;

        if (View.PrevButton != null) View.PrevButton.onClick.RemoveListener(HandlePrevClicked);
        if (View.NextButton != null) View.NextButton.onClick.RemoveListener(HandleNextClicked);
        if (View.ConfirmButton != null) View.ConfirmButton.onClick.RemoveListener(HandleConfirmClicked);
        if (View.TopCloseButton != null) View.TopCloseButton.onClick.RemoveListener(HandleConfirmClicked);
    }

    private void HandlePrevClicked()
    {
        if (Model == null || !Model.HasPreviousPage) return;

        SoundManager.Instance?.PlaySFX(SoundId.UI_Click);
        Model.CurrentPageIndex--;
        View?.RenderPage(Model);
    }

    private void HandleNextClicked()
    {
        if (Model == null || !Model.HasNextPage) return;

        SoundManager.Instance?.PlaySFX(SoundId.UI_Click);
        Model.CurrentPageIndex++;
        View?.RenderPage(Model);
    }

    private void HandleConfirmClicked()
    {
        View?.NotifyConfirmedAndClose();
    }
}