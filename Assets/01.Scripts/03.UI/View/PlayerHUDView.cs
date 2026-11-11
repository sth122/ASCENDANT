using Cysharp.Threading.Tasks;
using System.Threading;
/// <summary>
/// Player HUD 시각 뷰 컴포넌트
/// HP 및 스태미나의 즉각 소모와 잔상(Ghost) 감쇄 애니메이션을 처리하며,
/// 강인도(Poise) 수치를 모니터링합니다.
/// </summary>
public class PlayerHUDView : UIBase
{
    [UnityEngine.Header("Slider Components")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _hpSlider;
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _hpGhostSlider;
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _staminaSlider;
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _staminaGhostSlider;
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _poiseSlider;

    [UnityEngine.Header("Bar Root Transforms (가변 길이 대상)")]
    [UnityEngine.SerializeField] private UnityEngine.RectTransform _hpBarRect;
    [UnityEngine.SerializeField] private UnityEngine.RectTransform _staminaBarRect;
    [UnityEngine.SerializeField] private UnityEngine.RectTransform _poiseBarRect;

    [UnityEngine.Header("다크소울 스타일 길이 스케일링 설정")]
    [UnityEngine.SerializeField] private float _hpBaseWidth = 160f;
    [UnityEngine.SerializeField] private float _hpWidthPerPoint = 0.4f;

    [UnityEngine.SerializeField] private float _staminaBaseWidth = 120f;
    [UnityEngine.SerializeField] private float _staminaWidthPerPoint = 2.0f;

    [UnityEngine.SerializeField] private float _poiseBaseWidth = 135f;
    [UnityEngine.SerializeField] private float _poiseWidthPerPoint = 4.0f;

    [UnityEngine.Header("Ghost Slider Timings")]
    [UnityEngine.SerializeField] private float _ghostDelayDuration = 0.45f;
    [UnityEngine.SerializeField] private float _ghostDrainSpeed = 0.6f;

    [UnityEngine.Header("Interaction Prompt")]
    [UnityEngine.SerializeField] private UnityEngine.GameObject _promptRoot;
    [UnityEngine.SerializeField] private TMPro.TextMeshProUGUI _promptText;

    private CancellationTokenSource _hpGhostCts;
    private CancellationTokenSource _staminaGhostCts;

    // 중복 연산 방지용 캐시
    private float _cachedMaxHp = -1f;
    private float _cachedMaxStamina = -1f;
    private float _cachedMaxPoise = -1f;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.HUD;
    }

    /// <summary>
    /// 플레이어 스탯 계산 완료 시 1회만 호출되어 슬라이더 바의 가로폭을 설정
    /// </summary>
    public void SetBarWidths(float maxHp, float maxStamina, float maxPoise)
    {
        // 1. HP 바 가로폭 갱신
        if (_hpBarRect != null && !UnityEngine.Mathf.Approximately(_cachedMaxHp, maxHp) && maxHp > 0f)
        {
            _cachedMaxHp = maxHp;
            float targetWidth = _hpBaseWidth + (maxHp * _hpWidthPerPoint);
            _hpBarRect.sizeDelta = new UnityEngine.Vector2(targetWidth, _hpBarRect.sizeDelta.y);
        }

        // 2. 스태미나 바 가로폭 갱신
        if (_staminaBarRect != null && !UnityEngine.Mathf.Approximately(_cachedMaxStamina, maxStamina) && maxStamina > 0f)
        {
            _cachedMaxStamina = maxStamina;
            float targetWidth = _staminaBaseWidth + (maxStamina * _staminaWidthPerPoint);
            _staminaBarRect.sizeDelta = new UnityEngine.Vector2(targetWidth, _staminaBarRect.sizeDelta.y);
        }

        // 3. 강인도 바 가로폭 갱신
        if (_poiseBarRect != null && !UnityEngine.Mathf.Approximately(_cachedMaxPoise, maxPoise) && maxPoise > 0f)
        {
            _cachedMaxPoise = maxPoise;
            float targetWidth = _poiseBaseWidth + (maxPoise * _poiseWidthPerPoint);
            _poiseBarRect.sizeDelta = new UnityEngine.Vector2(targetWidth, _poiseBarRect.sizeDelta.y);
        }
    }

    /// <summary>
    /// 실시간 HP 변동 시 호출
    /// </summary>
    public void UpdateHp(float current, float max)
    {
        if (max <= 0f) return;

        float targetRatio = UnityEngine.Mathf.Clamp01(current / max);

        if (_hpSlider != null)
        {
            _hpSlider.value = targetRatio;
        }

        if (_hpGhostSlider != null)
        {
            if (targetRatio < _hpGhostSlider.value)
            {
                TriggerGhostDrain(ref _hpGhostCts, _hpGhostSlider, targetRatio);
            }
            else
            {
                AsyncUtil.CancelAndDispose(ref _hpGhostCts);
                _hpGhostSlider.value = targetRatio;
            }
        }
    }

    /// <summary>
    /// 실시간 스태미나 변동 시 호출
    /// </summary>
    public void UpdateStamina(float current, float max)
    {
        if (max <= 0f) return;

        float targetRatio = UnityEngine.Mathf.Clamp01(current / max);

        if (_staminaSlider != null)
        {
            _staminaSlider.value = targetRatio;
        }

        if (_staminaGhostSlider != null)
        {
            if (targetRatio < _staminaGhostSlider.value)
            {
                TriggerGhostDrain(ref _staminaGhostCts, _staminaGhostSlider, targetRatio);
            }
            else
            {
                AsyncUtil.CancelAndDispose(ref _staminaGhostCts);
                _staminaGhostSlider.value = targetRatio;
            }
        }
    }

    public void UpdatePoise(float current, float max)
    {
        if (max <= 0f || _poiseSlider == null) return;
        _poiseSlider.value = UnityEngine.Mathf.Clamp01(current / max);
    }

    private void TriggerGhostDrain(ref CancellationTokenSource cts, UnityEngine.UI.Slider ghostSlider, float targetRatio)
    {
        AsyncUtil.CancelAndDispose(ref cts);
        cts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);
        DrainGhostAsync(ghostSlider, targetRatio, cts.Token).Forget();
    }

    private async UniTaskVoid DrainGhostAsync(UnityEngine.UI.Slider ghostSlider, float targetRatio, CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(_ghostDelayDuration), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        while (ghostSlider.value > targetRatio)
        {
            isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (isCanceled) return;

            ghostSlider.value = UnityEngine.Mathf.MoveTowards(ghostSlider.value, targetRatio, _ghostDrainSpeed * UnityEngine.Time.unscaledDeltaTime);
        }

        ghostSlider.value = targetRatio;
    }

    public void SetInteractionPrompt(bool isVisible, string message = "")
    {
        if (_promptRoot != null)
        {
            _promptRoot.SetActive(isVisible);
        }

        if (_promptText != null && isVisible)
        {
            _promptText.text = message;
        }
    }

    protected override void OnDestroy()
    {
        AsyncUtil.CancelAndDispose(ref _hpGhostCts);
        AsyncUtil.CancelAndDispose(ref _staminaGhostCts);
        base.OnDestroy();
    }
}