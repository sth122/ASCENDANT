using Util = DebugLogger<FadeCurtainPresenter>;
/// <summary>
/// FadeCurtainView와 FadeCurtainModel을 중계하는 MVP Presenter 클래스
/// </summary>
public class FadeCurtainPresenter : UIPresenterBase<FadeCurtainView, FadeCurtainModel>
{
    protected override void OnBind()
    {
        Util.Log("[FadeCurtainPresenter 바인딩 완료]");
    }

    protected override void OnUnbind()
    {
        Util.Log("[FadeCurtainPresenter 해제 완료]");
    }
}