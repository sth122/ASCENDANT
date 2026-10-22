using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoSingleton<UIManager>, ILoadable
{
    public int LoadOrder => 10;

    [Header("Canvas Root")]
    [SerializeField] private Canvas _rootCanvas;

    private readonly Dictionary<UILayer, RectTransform> _layerContainers = new Dictionary<UILayer, RectTransform>();
    private readonly Dictionary<UIId, UIBase> _activeViews = new Dictionary<UIId, UIBase>();
    private readonly Dictionary<UIId, IUIPresenter> _activePresenters = new Dictionary<UIId, IUIPresenter>();

    private readonly Stack<UIWindowView> _windowStack = new Stack<UIWindowView>();
    private readonly Stack<UIBase> _popupStack = new Stack<UIBase>();

    private UIInputReader _uiInputReader;
    public UIInputReader InputReader => _uiInputReader;

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();

        _uiInputReader = new UIInputReader();
        _uiInputReader.OnCancelEvent += HandleCancelInput;

        InitializeCanvasLayers();
    }

    private void InitializeCanvasLayers()
    {
        if (_rootCanvas == null)
        {
            GameObject canvasGo = new GameObject("@UI_RootCanvas");
            canvasGo.transform.SetParent(transform);
            _rootCanvas = canvasGo.AddComponent<Canvas>();
            _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1.0f; // Height 고정으로 화면 비율에 무관한 크기 보장

            canvasGo.AddComponent<GraphicRaycaster>();
        }

        foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
        {
            GameObject container = new GameObject($"Layer_{layer}");
            RectTransform rect = container.AddComponent<RectTransform>();
            rect.SetParent(_rootCanvas.transform, false);

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Canvas layerCanvas = container.AddComponent<Canvas>();
            layerCanvas.overrideSorting = true;
            layerCanvas.sortingOrder = (int)layer;
            container.AddComponent<GraphicRaycaster>();

            _layerContainers[layer] = rect;
        }
    }

    #region ILoadable 구현부
    public UniTask OnSceneLoadCreate(SceneId scene) => UniTask.CompletedTask;
    public void Init(SceneId scene) => DebugLogger.LogWithTag("UIManager", $"[{scene}] UI 매니저 준비 완료");
    public void OnSceneDestroy(SceneId scene) => ClearSceneUIs();
    #endregion
    #region UI 오픈 및 생성 (MVP 바인딩)
    public async UniTask<TView> OpenUIAsync<TView, TPresenter, TModel>(UIId id, string addressableKey, TModel model = default, System.Threading.CancellationToken ct = default)
        where TView : UIBase
        where TPresenter : IUIPresenter, new()
        where TModel : IUIModel
    {
        if (_activeViews.TryGetValue(id, out UIBase existingView))
        {
            await existingView.ShowAsync(0.2f, ct);
            return existingView as TView;
        }

        GameObject prefab = await AssetsManager.Instance.LoadAssetAsync<GameObject>(addressableKey, ct);
        TView viewComp = prefab.GetComponent<TView>();
        RectTransform container = _layerContainers[viewComp.Layer];

        GameObject go = Instantiate(prefab, container);
        TView view = go.GetComponent<TView>();
        view.InitializeView();
        _activeViews[id] = view;

        TPresenter presenter = new TPresenter();
        presenter.Bind(view, model);
        _activePresenters[id] = presenter;

        await view.ShowAsync(0.2f, ct);

        if (view is UIWindowView window)
        {
            PushWindowToStack(window);
        }

        return view;
    }

    /// <summary>
    /// Presenter와 Model 없이 시각적 View만 단독으로 열 때 사용하는 경량 메서드
    /// </summary>
    public async UniTask<TView> OpenUIAsync<TView>(
        UIId id,
        string addressableKey,
        CancellationToken ct = default)
        where TView : UIBase
    {
        if (_activeViews.TryGetValue(id, out UIBase existingView))
        {
            await existingView.ShowAsync(0.2f, ct);
            return existingView as TView;
        }

        GameObject prefab = await AssetsManager.Instance.LoadAssetAsync<GameObject>(addressableKey, ct);
        TView viewComp = prefab.GetComponent<TView>();
        RectTransform container = _layerContainers[viewComp.Layer];

        GameObject go = Instantiate(prefab, container);
        TView view = go.GetComponent<TView>();
        view.InitializeView();
        _activeViews[id] = view;

        await view.ShowAsync(0.2f, ct);

        if (view is UIWindowView window)
        {
            PushWindowToStack(window);
        }

        return view;
    }

    public async UniTask<TResult> ShowPopupAsync<TPopup, TResult>(UIId id, string addressableKey) where TPopup : UIPopupView<TResult>
    {
        GameObject prefab = await AssetsManager.Instance.LoadAssetAsync<GameObject>(addressableKey);
        RectTransform container = _layerContainers[UILayer.Popup];
        GameObject go = Instantiate(prefab, container);
        TPopup popup = go.GetComponent<TPopup>();

        popup.InitializeView();
        _popupStack.Push(popup);
        _uiInputReader.EnableUIInput();

        TResult result = await popup.WaitForResultAsync();

        _popupStack.Pop();
        Destroy(go);

        if (_popupStack.Count == 0 && _windowStack.Count == 0)
        {
            _uiInputReader.EnablePlayerInput();
        }

        return result;
    }
    #endregion

    #region 윈도우 스택 및 취소(ESC) 처리
    private void PushWindowToStack(UIWindowView window)
    {
        if (!_windowStack.Contains(window))
        {
            _windowStack.Push(window);
        }

        _uiInputReader.EnableUIInput();
        if (window.PauseTimeWhenOpened) Time.timeScale = 0f;
    }

    public void NotifyWindowClosed(UIWindowView window)
    {
        if (_windowStack.Count > 0 && _windowStack.Peek() == window)
        {
            _windowStack.Pop();
        }

        if (window.PauseTimeWhenOpened) Time.timeScale = 1f;

        if (_windowStack.Count == 0 && _popupStack.Count == 0)
        {
            _uiInputReader.EnablePlayerInput();
        }
    }

    private void HandleCancelInput()
    {
        if (_popupStack.Count > 0)
        {
            UIBase topPopup = _popupStack.Peek();
            if (topPopup is UIPopupView<bool> boolPopup)
            {
                boolPopup.CancelPopup();
            }
            return;
        }

        if (_windowStack.Count > 0)
        {
            UIWindowView topWindow = _windowStack.Peek();
            topWindow.CloseWindowAsync().Forget();
        }
    }

    public void ClearSceneUIs()
    {
        List<UIId> toRemove = new List<UIId>();
        foreach (KeyValuePair<UIId, UIBase> pair in _activeViews)
        {
            if (!pair.Value.IsGlobalUI)
            {
                toRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            UIId id = toRemove[i];
            UIBase view = _activeViews[id];
            view.HideAsync(0f).Forget();

            if (_activePresenters.TryGetValue(id, out IUIPresenter presenter))
            {
                presenter.Unbind();
                _activePresenters.Remove(id);
            }

            _activeViews.Remove(id);
            Destroy(view.gameObject);
        }

        _windowStack.Clear();
        _popupStack.Clear();
        _uiInputReader.EnablePlayerInput();
    }
    #endregion

    protected override void OnDestroy()
    {
        if (_uiInputReader != null)
        {
            _uiInputReader.OnCancelEvent -= HandleCancelInput;
            _uiInputReader.Dispose();
        }
        base.OnDestroy();
    }
}