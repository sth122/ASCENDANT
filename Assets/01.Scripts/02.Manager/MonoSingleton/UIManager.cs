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
        _uiInputReader.OnInventoryToggleEvent += HandleInventoryToggle;
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
    /// 하이어라키 순서(Background -> World -> HUD -> Window -> Popup -> System)가 곧 렌더링 우선순위
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

            Canvas layerCanvas = containerGo.GetComponent<Canvas>();
            if (layerCanvas == null) layerCanvas = containerGo.AddComponent<Canvas>();
            layerCanvas.overrideSorting = true;
            layerCanvas.sortingOrder = (int)layer; // UILayer의 우선순위 정수 반영

            GraphicRaycaster raycaster = containerGo.GetComponent<GraphicRaycaster>();
            if (raycaster == null) containerGo.AddComponent<GraphicRaycaster>();

            _layerContainers[layer] = rect;
        }
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
    public async UniTask<TView> OpenUIAsync<TView, TPresenter, TModel>(UIId id, TModel model = default, System.Threading.CancellationToken ct = default)
        where TView : UIBase
        where TPresenter : IUIPresenter, new()
        where TModel : IUIModel
    {
        TView view = null;
        UILayer targetLayer = id.GetExpectedLayer();

        // 1. 이미 생성되어 캐싱된 뷰가 존재하는지 검사
        if (_activeViews.TryGetValue(id, out UIBase existingBase))
        {
            if (existingBase == null)
            {
                _activeViews.Remove(id);
                existingBase = null;
            }
            else if (existingBase is TView existingView)
            {
                view = existingView;
            }
        }

        if(view == null)
        {
            // 2. 신규 생성 파이프라인
            RectTransform targetContainer = GetLayerContainer(targetLayer);
            bool isGlobalLayer = (targetLayer == UILayer.System);
            GameObject viewGo = await AssetsManager.Instance.InstantiateAsync(id.GetAddressableKey(), parent: targetContainer, ct: ct);

            view = viewGo.GetComponent<TView>();
            if (view == null)
            {
                Util.LogError($"오브젝트({viewGo.name})에서 뷰 컴포넌트({typeof(TView).Name})를 찾을 수 없습니다.");
                Destroy(viewGo);
                return null;
            }

            RectTransform viewRect = view.GetComponent<RectTransform>();
            viewRect.SetParent(targetContainer, false);
            if (targetLayer != UILayer.HUD && targetLayer != UILayer.Popup)
            {
                SetFullStretch(viewRect);
            }

            view.SetIdentity(id, targetLayer);
            view.InitializeView();

            if (view.IsGlobalUI || isGlobalLayer)
            {
                AssetsManager.Instance.MarkInstanceAsGlobal(viewGo);
            }

            TPresenter presenter = new TPresenter();
            presenter.Bind(view, model);

            _activeViews[id] = view;
            _activePresenters[view] = presenter;
        }

        // 3. Window/Popup 스택 및 입력/일시정지 공통 적용
        if (targetLayer == UILayer.Window && view is UIWindowView windowView)
        {
            if (!_windowStack.Contains(windowView))
            {
                _windowStack.Push(windowView);
            }

            if (windowView.PausePolicy == WindowPausePolicy.PauseTime)
            {
                // 시스템 메뉴: 시간 정지 + 조작 차단
                if (GameManager.Instance != null) GameManager.Instance.PauseGame();
                SetInputFocusToUI(true);
            }
            else
            {
                // 실시간 인게임 창: 시간 정지 없음 (TimeScale = 1.0f 유지), 마우스 커서만 활성화
                _uiInputReader.EnableUIInput();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        else if (targetLayer == UILayer.Popup)
        {
            if (!_popupStack.Contains(view))
            {
                _popupStack.Push(view);
            }
            SetInputFocusToUI(true);
        }

        // 4. 시각적 페이드 연출 실행
        await view.ShowAsync(0.2f, ct);

        Util.Log($"[UI 오픈 완료] ID: {id} (Layer: {targetLayer}, Prefab: {id.GetAddressableKey()})");
        return view;
    }

    /// <summary>
    /// UI 창 개폐 시 플레이어 캐릭터와 UI 간의 조작권을 상호 스위칭
    /// </summary>
    public void SetInputFocusToUI(bool isUIFocused)
    {
        if (isUIFocused)
        {
            _uiInputReader.EnableUIInput();

            // 인게임 플레이어 조작권 및 이동 즉시 정지
            if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned && provider.Player != null)
            {
                provider.Player.InputReader?.DisablePlayerInput();
                provider.Player.StopMovementAPI();
            }
        }
        else
        {
            // 열려 있는 창이나 팝업이 더 이상 없을 때만 플레이어 입력 복원
            if (_windowStack.Count == 0 && _popupStack.Count == 0)
            {
                _uiInputReader.EnablePlayerInput();

                if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned && provider.Player != null)
                {
                    provider.Player.InputReader?.EnablePlayerInput();
                }
            }
        }
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

        if (window.PausePolicy == WindowPausePolicy.PauseTime && GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            GameManager.Instance.ResumeGame();
        }

        SetInputFocusToUI(false);
    }

    public void CloseUI(UIId id)
    {
        if (_activeViews.TryGetValue(id, out UIBase view))
        {
            if (view != null)
            {
                if (_activePresenters.TryGetValue(view, out IUIPresenter presenter))
                {
                    presenter.Unbind();
                    _activePresenters.Remove(view);
                }

                _activeViews.Remove(id);

                // Destroy 직접 호출 대신 AssetsManager를 통해 Addressables 인스턴스 핸들 해제 및 소멸
                if (AssetsManager.Instance != null)
                {
                    AssetsManager.Instance.ReleaseInstance(view.gameObject);
                }
                else
                {
                    Destroy(view.gameObject);
                }
            }
            else
            {
                _activeViews.Remove(id);
            }
        }
    }
    #endregion

    public void NotifyPopupClosed(UIBase popup)
    {
        if (popup == null) return;

        if (_popupStack.Count > 0 && _popupStack.Peek() == popup)
        {
            _popupStack.Pop();
        }

        SetInputFocusToUI(false);
    }

    #region ESC 입력 및 윈도우 스택 관리
    public bool HasActiveModalOrWindow() => _popupStack.Count > 0 || _windowStack.Count > 0;

    private void HandleCancelInput()
    {
        // 1. 팝업이 떠 있는 경우 최상단 팝업 취소
        if (_popupStack.Count > 0)
        {
            UIBase topPopup = _popupStack.Peek();
            if (topPopup is ConfirmPopupView confirmPopup)
            {
                confirmPopup.CancelPopup();
            }
            else
            {
                _popupStack.Pop();
                topPopup.HideAsync(0.15f).Forget();
                SetInputFocusToUI(false);
            }
            return;
        }

        // 2. 윈도우 창이 열려 있는 경우
        if (_windowStack.Count > 0)
        {
            UIWindowView topWindow = _windowStack.Peek();
            topWindow.CloseWindowAsync().Forget();
        }
    }

    private void HandleInventoryToggle()
    {
        if (_activeViews.TryGetValue(UIId.InventoryUI, out UIBase invView) && invView.gameObject.activeSelf)
        {
            if (invView is UIWindowView win)
            {
                win.CloseWindowAsync().Forget();
            }
        }
    }

    public void CloseAllSceneDependentViews()
    {
        List<UIId> toRemove = new List<UIId>();

        foreach (KeyValuePair<UIId, UIBase> pair in _activeViews)
        {
            // 뷰가 이미 소멸되었거나 씬 종속적인 뷰인 경우 정리
            if (pair.Value == null || !pair.Value.IsGlobalUI)
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
            _uiInputReader.OnInventoryToggleEvent -= HandleInventoryToggle;
            _uiInputReader.Dispose();
        }
        base.OnDestroy();
    }
    #endregion
}