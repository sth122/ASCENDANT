/// <summary>
/// Player HUD 시각 뷰 컴포넌트
/// HP 및 스태미나의 즉각 소모와 잔상(Ghost) 감쇄 애니메이션을 처리하며,
/// 강인도(Poise) 수치를 모니터링합니다.
/// </summary>
public class PlayerHUDView : UIBase
{
    [UnityEngine.Header("HP Gauge Sliders")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _hpSlider;
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _hpGhostSlider;

    [UnityEngine.Header("Stamina Gauge Sliders")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _staminaSlider;
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _staminaGhostSlider;

    [UnityEngine.Header("Poise Gauge Sliders")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Slider _poiseSlider;

    [UnityEngine.Header("Ghost Aniamtion Settings")]
    [UnityEngine.SerializeField] private float _ghostDelayDuration = 0.45f;
    [UnityEngine.SerializeField] private float _ghostDrainSpeed = 0.6f;

    private float _hpGhostTimer = 0f;
    private float _staminaGhostTimer = 0f;
    private float _previousHpRatio = 1.0f;
    private float _previousStaminaRatio = 1.0f;

    public void UpdateHpBar(float current, float max)
    {
        if (_hpSlider == null || max <= 0f) return;

        float targetRatio = UnityEngine.Mathf.Clamp01(current / max);

        // 1. 피해를 입었을 때: 전면 슬라이더는 즉시 갱신, 후면 잔상은 대기 타이머 세팅
        if (targetRatio < _previousHpRatio)
        {
            _hpSlider.value = targetRatio;
            _hpGhostTimer = _ghostDelayDuration;
        }
        // 2. 회복되었을 때: 전면과 후면을 즉시 동기화
        else
        {
            _hpSlider.value = targetRatio;
            if (_hpGhostSlider != null)
            {
                _hpGhostSlider.value = targetRatio;
            }
            _hpGhostTimer = 0f;
        }

        _previousHpRatio = targetRatio;
    }

    public void UpdateStaminaBar(float current, float max)
    {
        if (_staminaSlider == null || max <= 0f) return;

        float targetRatio = UnityEngine.Mathf.Clamp01(current / max);
        float delta = _previousStaminaRatio - targetRatio;

        // 1. 단발성 상용 소모(공격, 점프, 구르기 등 프레임당 3% 이상 급감 시)
        if (delta > 0.03f)
        {
            _staminaSlider.value = targetRatio;
            _staminaGhostTimer = _ghostDelayDuration;
        }
        // 2. 지속형 감산(달리기) 또는 회복 중일 때: 지연 없이 부드럽게 추종
        else
        {
            _staminaSlider.value = targetRatio;
            if (_staminaGhostTimer <= 0f && _staminaGhostSlider != null)
            {
                _staminaGhostSlider.value = targetRatio;
            }
        }

        _previousStaminaRatio = targetRatio;
    }

    public void UpdatePoiseBar(float current, float max)
    {
        if (_poiseSlider != null && max > 0f)
        {
            _poiseSlider.value = UnityEngine.Mathf.Clamp01(current / max);
        }
    }

    private void Update()
    {
        UpdateHpGhostDrain();
        UpdateStaminaGhostDrain();
    }

    /// <summary>
    /// HP 후면 잔상 슬라이더 감쇄 연산
    /// </summary>
    private void UpdateHpGhostDrain()
    {
        if (_hpGhostSlider == null || _hpSlider == null) return;

        if (_hpGhostTimer > 0f)
        {
            _hpGhostTimer -= UnityEngine.Time.unscaledDeltaTime;
        }
        else if (_hpGhostSlider.value > _hpSlider.value)
        {
            _hpGhostSlider.value = UnityEngine.Mathf.MoveTowards(
                _hpGhostSlider.value,
                _hpSlider.value,
                _ghostDrainSpeed * UnityEngine.Time.unscaledDeltaTime
            );
        }
    }

    /// <summary>
    /// 스태미나 후면 잔상 슬라이더 감쇄 연산
    /// </summary>
    private void UpdateStaminaGhostDrain()
    {
        if (_staminaGhostSlider == null || _staminaSlider == null) return;

        if (_staminaGhostTimer > 0f)
        {
            _staminaGhostTimer -= UnityEngine.Time.unscaledDeltaTime;
        }
        else if (_staminaGhostSlider.value > _staminaSlider.value)
        {
            _staminaGhostSlider.value = UnityEngine.Mathf.MoveTowards(
                _staminaGhostSlider.value,
                _staminaSlider.value,
                _ghostDrainSpeed * UnityEngine.Time.unscaledDeltaTime
            );
        }
    }
}