/// <summary>
/// MVP 패턴의 Presenter 추상 베이스 클래스
/// </summary>
public abstract class UIPresenterBase<TView, TModel> : IUIPresenter where TView : UIBase where TModel : IUIModel
{
    protected TView View { get; private set; }
    protected TModel Model { get; private set; }

    public void Bind(UIBase view, IUIModel model)
    {
        View = view as TView;
        Model = (TModel)model;
        OnBind();
    }

    public void Unbind()
    {
        OnUnbind();
        View = null;
        Model = default;
    }

    protected abstract void OnBind();
    protected abstract void OnUnbind();
}