using Cysharp.Threading.Tasks;

/// <summary>
/// 플레이어 사망 시 화면 전체에 연출되는 게임오버 UI 뷰
/// </summary>
public class GameOverView : UIWindowView
{
    [UnityEngine.Header("Title Text & Animation")]
    [UnityEngine.SerializeField] private TMPro.TextMeshProUGUI _youDiedText;
    [UnityEngine.SerializeField] private UnityEngine.CanvasGroup _buttonGroup;

    [UnityEngine.Header("Buttons")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _respawnButton;
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _titleButton;

    public UnityEngine.UI.Button RespawnButton => _respawnButton;
    public UnityEngine.UI.Button TitleButton => _titleButton;

    public override void InitializeView()
    {
        base.InitializeView();
        _pauseTimeWhenOpened = false; // 사망 후 슬로우 모션 연출 보존을 위해 timeScale 직접 조작 방지
    }

    /// <summary>
    /// YOU DIED 텍스트 서서히 등장 후 하단 버튼 페이드인
    /// </summary>
    public override async UniTask ShowAsync(float duration = 1.5f, System.Threading.CancellationToken ct = default)
    {
        CreateActionToken(ct);
        gameObject.SetActive(true);
        IsVisible = true;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
        }

        // 1. 버튼 그룹 숨김
        if (_buttonGroup != null)
        {
            _buttonGroup.alpha = 0f;
            _buttonGroup.interactable = false;
            _buttonGroup.blocksRaycasts = false;
        }

        // 2. YOU DIED 텍스트 붉은색 알파 페이드인
        if (_youDiedText != null)
        {
            UnityEngine.Color c = _youDiedText.color;
            c.a = 0f;
            _youDiedText.color = c;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, _fadeCts.Token).SuppressCancellationThrow();
                if (isCanceled) return;

                elapsed += UnityEngine.Time.unscaledDeltaTime;
                c.a = UnityEngine.Mathf.Clamp01(elapsed / duration);
                _youDiedText.color = c;
            }
            c.a = 1f;
            _youDiedText.color = c;
        }

        // 3. 0.5초 대기 후 조작 버튼 활성화
        await UniTask.Delay(500, ignoreTimeScale: true, cancellationToken: _fadeCts.Token).SuppressCancellationThrow();

        if (_buttonGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, _fadeCts.Token).SuppressCancellationThrow();
                if (isCanceled) return;

                elapsed += UnityEngine.Time.unscaledDeltaTime;
                _buttonGroup.alpha = UnityEngine.Mathf.Clamp01(elapsed / 0.5f);
            }
            _buttonGroup.alpha = 1f;
            _buttonGroup.interactable = true;
            _buttonGroup.blocksRaycasts = true;
        }
    }
}