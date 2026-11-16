// TitleBackgroundPresenter.cs
public class TitleBackgroundPresenter : UIPresenterBase<TitleBackgroundView, TitleBackgroundModel>
{
    protected override void OnBind()
    {
        if (View != null && Model != null)
        {
            View.ConfigureMotion(Model.BreathZoomAmplitude, Model.BreathZoomSpeed);
        }
    }

    protected override void OnUnbind()
    {
        Model?.Reset();
    }
}