using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 모든 UI의 최상위 추상 클래스
/// </summary>
public class UIBase : MonoBehaviour, IPoolable
{
    [field: Header("UI Configuration")]
    [field: SerializeField] public UIId Identifier { get; protected set; }
    [field: SerializeField] public UILayer Layer { get; protected set; }
    [field: SerializeField] public bool IsGlobalUI { get; protected set; } = false;

    protected CanvasGroup _canvasGroup;
    protected RectTransform _rectTransform;
    private System.Threading.CancellationTokenSource _fadeCts;
    public bool IsVisible { get; private set; } = false;

    protected virtual void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// UIManager에 의해 생성될 때 UIId 번호 대역에 맞춰 식별자와 레이어를 강제 동기화
    /// </summary>
    public void SetIdentity(UIId identifier, UILayer layer)
    {
        Identifier = identifier;
        Layer = layer;
    }

    public virtual void InitializeView() { }
    /// <summary>
    /// UniTask 기반의 부드러운 화면 출력
    /// </summary>
    public virtual async UniTask ShowAsync(float duration = 0.2f, System.Threading.CancellationToken ct = default)
    {
        CreateActionToken(ct);

        gameObject.SetActive(true);
        IsVisible = true;

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;

            if (duration > 0f)
            {
                await FadeCanvasGroupAsync(1f, duration, _fadeCts.Token);
            }
            else
            {
                _canvasGroup.alpha = 1f;
            }
        }
    }

    /// <summary>
    /// UniTask 기반의 부드러운 화면 숨김
    /// </summary>
    public virtual async UniTask HideAsync(float duration = 0.15f, System.Threading.CancellationToken ct = default)
    {
        CreateActionToken(ct);

        IsVisible = false;

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            if (duration > 0f)
            {
                await FadeCanvasGroupAsync(0f, duration, _fadeCts.Token);
            }
            else
            {
                _canvasGroup.alpha = 0f;
            }
        }

        gameObject.SetActive(false);
    }

    protected async UniTask FadeCanvasGroupAsync(float targetAlpha, float duration, System.Threading.CancellationToken ct)
    {
        float startAlpha = _canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
            if (isCanceled) return;

            elapsed += Time.unscaledDeltaTime; // 일시정지(TimeScale = 0) 시에도 동작하도록 unscaled 사용
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
        }

        _canvasGroup.alpha = targetAlpha;
    }
    protected void CancelFadeToken() => AsyncUtil.CancelAndDispose(ref _fadeCts);

    protected System.Threading.CancellationToken CreateActionToken(System.Threading.CancellationToken ct = default)
    {
        CancelFadeToken();
        _fadeCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct, this.destroyCancellationToken);
        return _fadeCts.Token;
    }

    #region IPoolable 구현부
    public virtual void OnSpawn() => ShowAsync(0f).Forget();

    public virtual void OnDespawn() => HideAsync(0f).Forget();
    #endregion

    protected virtual void OnDestroy() => CancelFadeToken();
}
