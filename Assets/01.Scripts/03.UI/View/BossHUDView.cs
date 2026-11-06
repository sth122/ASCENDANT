using UnityEngine;

/// <summary>
/// 화면 중앙 하단에 표시되는 보스 전용 대형 HUD 뷰
/// </summary>
public class BossHUDView : UIBase
{
    [Header("Boss Title")]
    [SerializeField] private TMPro.TextMeshProUGUI _bossNameText;

    [Header("HP Sliders")]
    [SerializeField] private UnityEngine.UI.Slider _hpSlider;
    [SerializeField] private UnityEngine.UI.Slider _hpGhostSlider;

    [Header("Poise Sliders")]
    [SerializeField] private UnityEngine.UI.Slider _poiseSlider;
    [SerializeField] private UnityEngine.UI.Image _poiseFillImage;
    [SerializeField] private Color _normalPoiseColor = new Color(0.9f, 0.7f, 0.1f, 1f);
    [SerializeField] private Color _groggyPoiseColor = new Color(0.9f, 0.1f, 0.1f, 1f);

    [Header("Ghost Animation Settings")]
    [SerializeField] private float _ghostDelayDuration = 0.5f;
    [SerializeField] private float _ghostDrainSpeed = 0.4f;

    private float _hpGhostTimer = 0f;
    private float _previousHpRatio = 1.0f;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.HUD;
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }
    }

    public void SetBossIdentity(string bossName, float currentHp, float maxHp, float currentPoise, float maxPoise)
    {
        if (_bossNameText != null)
        {
            _bossNameText.text = bossName;
        }

        _previousHpRatio = Mathf.Clamp01(currentHp / maxHp);
        if (_hpSlider != null) _hpSlider.value = _previousHpRatio;
        if (_hpGhostSlider != null) _hpGhostSlider.value = _previousHpRatio;

        UpdatePoise(currentPoise, maxPoise);
    }

    public void UpdateHp(float current, float max)
    {
        if (_hpSlider == null || max <= 0f) return;

        float targetRatio = Mathf.Clamp01(current / max);

        if (targetRatio < _previousHpRatio)
        {
            _hpSlider.value = targetRatio;
            _hpGhostTimer = _ghostDelayDuration;
        }
        else
        {
            _hpSlider.value = targetRatio;
            if (_hpGhostSlider != null) _hpGhostSlider.value = targetRatio;
            _hpGhostTimer = 0f;
        }

        _previousHpRatio = targetRatio;
    }

    public void UpdatePoise(float current, float max)
    {
        if (_poiseSlider == null || max <= 0f) return;

        float ratio = Mathf.Clamp01(current / max);
        _poiseSlider.value = ratio;

        if (_poiseFillImage != null)
        {
            _poiseFillImage.color = (ratio <= 0.001f) ? _groggyPoiseColor : _normalPoiseColor;
        }
    }

    private void Update()
    {
        if (_hpGhostSlider != null && _hpSlider != null)
        {
            if (_hpGhostTimer > 0f)
            {
                _hpGhostTimer -= Time.unscaledDeltaTime;
            }
            else if (_hpGhostSlider.value > _hpSlider.value)
            {
                _hpGhostSlider.value = Mathf.MoveTowards(
                    _hpGhostSlider.value,
                    _hpSlider.value,
                    _ghostDrainSpeed * Time.unscaledDeltaTime
                );
            }
        }
    }
}