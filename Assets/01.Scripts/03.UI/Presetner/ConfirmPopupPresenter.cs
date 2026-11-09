using Util = DebugLogger<ConfirmPopupPresenter>;

/// <summary>
/// ConfirmPopupView와 ConfirmPopupModel을 중계하는 MVP Presenter 클래스
/// </summary>
public class ConfirmPopupPresenter : UIPresenterBase<ConfirmPopupView, ConfirmPopupModel>
{
    protected override void OnBind()
    {
        if (View == null) return;

        // Model에 사전 정의된 메시지가 있다면 View에 즉각 반영
        if (Model != null && !string.IsNullOrEmpty(Model.Message))
        {
            View.SetMessage(Model.Message);
        }

        Util.Log($"[ConfirmPopupPresenter 바인딩 완료] 메시지: {Model?.Message}");
    }

    protected override void OnUnbind()
    {
        Util.Log("[ConfirmPopupPresenter 해제 완료]");
    }
}