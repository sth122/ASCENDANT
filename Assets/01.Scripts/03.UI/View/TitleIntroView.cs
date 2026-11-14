using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(CanvasGroup))]
public class TitleIntroView : UIBase
{
    [Header("Intro UI References")]
    [SerializeField] private CanvasGroup _rootCanvasGroup;
    [SerializeField] private CanvasGroup _curtainCanvasGroup;
    [SerializeField] private RectTransform _logoRect;
    [SerializeField] private CanvasGroup _logoCanvasGroup;
    [SerializeField] private TMPro.TextMeshProUGUI _pressAnyButtonText;

    [Header("Logo Transition Positions")]
    [SerializeField] private Vector2 _centerLogoPos = new Vector2(0f, 30f);
    [SerializeField] private Vector2 _topLogoPos = new Vector2(0f, 310f);
    [SerializeField] private Vector3 _centerLogoScale = Vector3.one;

    protected override void Awake()
    {
        if (_rootCanvasGroup == null) _rootCanvasGroup = GetComponent<CanvasGroup>();

        // 0ms 첫 프레임부터 검은 화면 유지, 로고는 투명 상태에서 시작
        if (_curtainCanvasGroup != null)
        {
            _curtainCanvasGroup.alpha = 1f;
            _curtainCanvasGroup.blocksRaycasts = true;
        }
        if (_logoCanvasGroup != null) _logoCanvasGroup.alpha = 0f;
        if (_logoRect != null)
        {
            _logoRect.anchoredPosition = _centerLogoPos;
            _logoRect.localScale = _centerLogoScale;
        }
        if (_pressAnyButtonText != null) _pressAnyButtonText.gameObject.SetActive(false);

        EventBusSystem.Subscribe<TitleIntroVisibilityEvent>(HandleVisibilityChanged);
    }

    public async UniTask FadeInCenterLogoAsync(float duration, CancellationToken ct)
    {
        if (_logoCanvasGroup == null) return;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
            if (canceled) return;

            elapsed += Time.unscaledDeltaTime;
            _logoCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, elapsed / duration);
        }
        _logoCanvasGroup.alpha = 1f;
    }

    public async UniTask WaitForAnyButtonAsync(CancellationToken ct)
    {
        if (_pressAnyButtonText != null) _pressAnyButtonText.gameObject.SetActive(true);

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.15f), ignoreTimeScale: true, cancellationToken: ct);

        float pulseTimer = 0f;
        while (!ct.IsCancellationRequested)
        {
            pulseTimer += Time.unscaledDeltaTime * 2.5f;
            if (_pressAnyButtonText != null)
            {
                Color c = _pressAnyButtonText.color;
                c.a = Mathf.Lerp(0.25f, 1.0f, (Mathf.Sin(pulseTimer) + 1f) * 0.5f);
                _pressAnyButtonText.color = c;
            }

            if (IsAnyInputTriggered())
            {
                SoundManager.Instance?.PlaySFX(SoundId.UI_Confirm);
                break;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        if (_pressAnyButtonText != null) _pressAnyButtonText.gameObject.SetActive(false);
    }

    public async UniTask TransitionLogoToHeaderAsync(float duration, CancellationToken ct)
    {
        float elapsed = 0f;
        Vector2 startPos = _logoRect != null ? _logoRect.anchoredPosition : _centerLogoPos;
        Vector3 startScale = _logoRect != null ? _logoRect.localScale : _centerLogoScale;

        while (elapsed < duration)
        {
            bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
            if (canceled) return;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            if (_logoRect != null)
            {
                _logoRect.anchoredPosition = Vector2.Lerp(startPos, _topLogoPos, t);
            }

            if (_curtainCanvasGroup != null) _curtainCanvasGroup.alpha = 1f - t;
        }

        if (_logoRect != null)
        {
            _logoRect.anchoredPosition = _topLogoPos;
        }

        if (_curtainCanvasGroup != null)
        {
            _curtainCanvasGroup.alpha = 0f;
            _curtainCanvasGroup.blocksRaycasts = false;
        }

        if (_rootCanvasGroup != null)
        {
            _rootCanvasGroup.blocksRaycasts = false;
            _rootCanvasGroup.interactable = false;
        }
    }

    private bool IsAnyInputTriggered()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;

        if (Gamepad.current != null)
        {
            var controls = Gamepad.current.allControls;
            for (int i = 0; i < controls.Count; i++)
            {
                if (controls[i] is ButtonControl btn && btn.wasPressedThisFrame) return true;
            }
        }
        return false;
    }

    private void HandleVisibilityChanged(TitleIntroVisibilityEvent evt) => gameObject.SetActive(evt.IsVisible);

    protected override void OnDestroy()
    {
        EventBusSystem.Unsubscribe<TitleIntroVisibilityEvent>(HandleVisibilityChanged);
        base.OnDestroy();
    }
}