using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Util = DebugLogger<UIManager>;

public class UIManager : MonoSingleton<UIManager>, ILoadable
{
    public int LoadOrder => 10;

    [Header("Canvas Root")]
    [SerializeField] private Canvas _rootCanvas;
    [SerializeField] private CanvasScaler _canvasScaler;

    private readonly Dictionary<UILayer, RectTransform> _layerContainers = new Dictionary<UILayer, RectTransform>();
    private readonly Dictionary<UIId, UIBase> _activeViews = new Dictionary<UIId, UIBase>();
    private readonly Dictionary<UIBase, IUIPresenter> _activePresenters = new Dictionary<UIBase, IUIPresenter>();

    private readonly Stack<UIWindowView> _windowStack = new Stack<UIWindowView>();
    private readonly Stack<UIBase> _popupStack = new Stack<UIBase>();

    private UIInputReader _uiInputReader;
    public UIInputReader InputReader => _uiInputReader;

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();

        EnsureRootCanvas();
        CreateLayerContainers();

        _uiInputReader = new UIInputReader();
        _uiInputReader.OnCancelEvent += HandleCancelInput;
    }

    #region Root Canvas & Layer Containers 자동 구축
    private void EnsureRootCanvas()
    {
        if (_rootCanvas != null) return;

        GameObject canvasGo = new GameObject("@UI_RootCanvas");
        canvasGo.transform.SetParent(transform);

        _rootCanvas = canvasGo.AddComponent<Canvas>();
        _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _rootCanvas.sortingOrder = 100;

        _canvasScaler = canvasGo.AddComponent<CanvasScaler>();
        _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _canvasScaler.referenceResolution = new Vector2(1920, 1080);
        _canvasScaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// UILayer Enum 순서에 따라 캔버스 하위에 정렬용 컨테이너 생성
    /// 하이어라키 순서(Background -> World -> HUD -> Window -> Popup -> System)가 곧 렌더링 우선순위가 됩니다.
    /// </summary>
    private void CreateLayerContainers()
    {
        _layerContainers.Clear();

        UILayer[] layers = (UILayer[])Enum.GetValues(typeof(UILayer));
        for (int i = 0; i < layers.Length; i++)
        {
            UILayer layer = layers[i];
            string containerName = $"@Layer_{layer}";

            Transform existing = _rootCanvas.transform.Find(containerName);
            GameObject containerGo = existing != null ? existing.gameObject : new GameObject(containerName, typeof(RectTransform));
            containerGo.transform.SetParent(_rootCanvas.transform, false);

            RectTransform rect = containerGo.GetComponent<RectTransform>();
            SetFullStretch(rect);

            _layerContainers[layer] = rect;
        }

        Util.Log($"총 {_layerContainers.Count}개 UILayer 컨테이너 구축 완료");
    }

    private void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
    #endregion

    #region ILoadable 구현부 - 손 볼 예정
    public UniTask OnSceneLoadCreate(SceneId scene) => UniTask.CompletedTask;
    public void Init(SceneId scene) => DebugLogger.LogWithTag("UIManager", $"[{scene}] UI 매니저 준비 완료");
    public void OnSceneDestroy(SceneId scene) => CloseAllSceneDependentViews();
    #endregion


    #region UIId 기반 자동 배치 OpenUIAsync
    /// <summary>
    /// UIId 숫자 대역을 분석하여 해당 레이어 컨테이너에 자동 배치하고 Presenter를 바인딩
    /// </summary>
    public async UniTask<TView> OpenUIAsync<TView, TPresenter, TModel>(UIId id, string addressableKey, TModel model = default, System.Threading.CancellationToken ct = default)
        where TView : UIBase
        where TPresenter : IUIPresenter, new()
        where TModel : IUIModel
    {
        TView view;
        UILayer targetLayer = id.GetExpectedLayer();

        // 1. 이미 생성되어 캐싱된 뷰가 존재하는지 검사
        if (_activeViews.TryGetValue(id, out UIBase existingBase) && existingBase is TView existingView)
        {
            view = existingView;
        }

        else
        {
            // 2. 신규 생성 파이프라인
            RectTransform targetContainer = GetLayerContainer(targetLayer);
            GameObject viewGo = await AssetsManager.Instance.InstantiateAsync(addressableKey, parent: targetContainer, ct: ct);
            if (viewGo == null)
            {
                Util.LogError($"UI 에셋 로드 및 인스턴스화 실패: {addressableKey} (ID: {id})");
                return null;
            }

            view = viewGo.GetComponent<TView>();
            if (view == null)
            {
                Util.LogError($"오브젝트({viewGo.name})에서 뷰 컴포넌트({typeof(TView).Name})를 찾을 수 없습니다.");
                Destroy(viewGo);
                return null;
            }

            RectTransform viewRect = view.GetComponent<RectTransform>();
            viewRect.SetParent(targetContainer, false);
            SetFullStretch(viewRect);

            view.SetIdentity(id, targetLayer);
            view.InitializeView();

            TPresenter presenter = new TPresenter();
            presenter.Bind(view, model);

            _activeViews[id] = view;
            _activePresenters[view] = presenter;
        }

        // 3. [핵심 해결] 캐시 재사용 여부와 무관하게 Window/Popup 스택 및 입력/일시정지 정책 공통 적용
        if (targetLayer == UILayer.Window && view is UIWindowView windowView)
        {
            if (!_windowStack.Contains(windowView))
            {
                _windowStack.Push(windowView);
            }

            _uiInputReader.EnableUIInput();

            if (windowView.PauseTimeWhenOpened && GameManager.Instance != null)
            {
                GameManager.Instance.PauseGame();
            }
        }
        else if (targetLayer == UILayer.Popup)
        {
            if (!_popupStack.Contains(view))
            {
                _popupStack.Push(view);
            }
            _uiInputReader.EnableUIInput();
        }

        // 4. 시각적 페이드 연출 실행
        await view.ShowAsync(0.2f, ct);

        Util.Log($"[UI 오픈 완료] ID: {id} (Layer: {targetLayer}, Prefab: {addressableKey})");
        return view;
    }

    public RectTransform GetLayerContainer(UILayer layer)
    {
        if (_layerContainers.TryGetValue(layer, out RectTransform container))
        {
            return container;
        }
        return _rootCanvas != null ? _rootCanvas.transform as RectTransform : null;
    }

    public void NotifyWindowClosed(UIWindowView window)
    {
        if (window == null) return;

        if (_windowStack.Count > 0 && _windowStack.Peek() == window)
        {
            _windowStack.Pop();
        }

        // 모든 윈도우와 팝업이 닫혔다면 인게임 플레이 조작권으로 복구
        if (_windowStack.Count == 0 && _popupStack.Count == 0)
        {
            _uiInputReader.EnablePlayerInput();

            if (window.PauseTimeWhenOpened && GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                GameManager.Instance.ResumeGame();
            }
        }
    }

    public void CloseUI(UIId id)
    {
        if (_activeViews.TryGetValue(id, out UIBase view))
        {
            if (_activePresenters.TryGetValue(view, out IUIPresenter presenter))
            {
                presenter.Unbind();
                _activePresenters.Remove(view);
            }

            _activeViews.Remove(id);
            Destroy(view.gameObject);
        }
    }
    #endregion

    #region ESC 입력 및 윈도우 스택 관리
    public bool HasActiveModalOrWindow() => _popupStack.Count > 0 || _windowStack.Count > 0;

    private void HandleCancelInput()
    {
        // 1. 팝업이 떠 있는 경우 최상단 팝업 취소
        if (_popupStack.Count > 0)
        {
            UIBase topPopup = _popupStack.Pop();
            topPopup.HideAsync(0.15f).Forget();
            return;
        }

        // 2. 윈도우 창이 열려 있는 경우
        if (_windowStack.Count > 0)
        {
            UIWindowView topWindow = _windowStack.Peek();
            topWindow.CloseWindowAsync().Forget();
            return;
        }
    }

    public void CloseAllSceneDependentViews()
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
            CloseUI(toRemove[i]);
        }

        _windowStack.Clear();
        _popupStack.Clear();
    }


    protected override void OnDestroy()
    {
        if (_uiInputReader != null)
        {
            _uiInputReader.OnCancelEvent -= HandleCancelInput;
            _uiInputReader.Dispose();
        }
        base.OnDestroy();
    }
    #endregion
}