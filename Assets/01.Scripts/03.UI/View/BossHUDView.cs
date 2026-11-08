using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 중앙 하단에 표시되는 보스 전용 대형 HUD 뷰
/// </summary>
public class BossHUDView : UIBase
{
    [Header("Boss Identity")]
    [SerializeField] private UnityEngine.UI.Text _bossNameText;
    [SerializeField] private UnityEngine.UI.Image _bossPortraitImage;

    [Header("HP Sliders")]
    [SerializeField] private UnityEngine.UI.Slider _hpSlider;
    [Tooltip("잔상 연출용 슬라이더 ")]
    [SerializeField] private UnityEngine.UI.Slider _hpGhostSlider;

    [Header("Poise Sliders")]
    [SerializeField] private UnityEngine.UI.Slider _poiseSlider;
    [SerializeField] private UnityEngine.UI.Image _poiseFillImage;
    [SerializeField] private Color _normalPoiseColor = new Color(0.05f, 0.45f, 0.84f, 1f); // 프리팹 기본 파란색
    [SerializeField] private Color _groggyPoiseColor = new Color(0.9f, 0.1f, 0.1f, 1f);   // 그로기 위험 붉은색

    [Header("Ghost Drain Settings")]
    [SerializeField] private float _ghostDelayDuration = 0.5f;
    [SerializeField] private float _ghostDrainSpeed = 0.4f;

    private float _hpGhostTimer = 0f;
    private float _previousHpRatio = 1.0f;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.HUD;

        SetupLayout();

        // 시작 시 화면에서 보이지 않도록 숨김 대기
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
        gameObject.SetActive(false);
    }

    private void SetupLayout()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            // 1. 하단 중앙 앵커 (Bottom-Center)
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);

            // 2. 다크 소울 표준 크기: 가로 1000px, 세로 85px, 바닥에서 80px 띄움
            rt.sizeDelta = new Vector2(1000f, 85f);
            rt.anchoredPosition = new Vector2(0f, 80f);
            rt.localScale = Vector3.one;
        }

        // 3. 자식 요소들을 하단 중앙 기준으로 정렬
        if (TryGetComponent<VerticalLayoutGroup>(out var vlg))
        {
            vlg.childAlignment = TextAnchor.LowerCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
        }
    }

    /// <summary>
    /// 조우 시 보스 정보 및 초기 수치 세팅
    /// </summary>
    public void SetBossIdentity(string bossName, Sprite portrait, float currentHp, float maxHp, float currentPoise, float maxPoise)
    {
        if (_bossNameText != null)
        {
            _bossNameText.text = bossName;
        }

        if (_bossPortraitImage != null && portrait != null)
        {
            _bossPortraitImage.sprite = portrait;
        }

        _previousHpRatio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 1.0f;
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
        // HP 잔상 슬라이더 감쇄 처리
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