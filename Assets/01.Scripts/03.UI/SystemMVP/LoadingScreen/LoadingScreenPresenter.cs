/// <summary>
/// LoadingScreenView와 LoadingScreenModel을 연결하고 로딩 이벤트를 수신하는 Presenter
/// </summary>
public class LoadingScreenPresenter : UIPresenterBase<LoadingScreenView, LoadingScreenModel>
{
    protected override void OnBind()
    {
        OnBindHandler();
        if (View != null && Model != null)
        {
            View.UpdateProgress(Model.Progress, Model.DescriptionText);
        }
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
        Model?.Reset();
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();
        EventBusSystem.Subscribe<SceneLoadingProgressEvent>(HandleLoadingProgress);
    }

    protected override void OnUnbindHandler()
    {
        EventBusSystem.Unsubscribe<SceneLoadingProgressEvent>(HandleLoadingProgress);
    }

    private void HandleLoadingProgress(SceneLoadingProgressEvent evt)
    {
        if (Model != null)
        {
            Model.SetProgress(evt.Progress, evt.Description);
        }

        if (View != null)
        {
            View.UpdateProgress(evt.Progress, evt.Description);
        }
    }
}