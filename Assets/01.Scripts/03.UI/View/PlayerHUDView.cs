using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 플레이어 상태(HP, 스태미나, 강인도 메인/고스트 게이지 및 프레임 길이)와
/// 하단 상호작용 프롬프트(단일 안내 + 다중 선택지)를 통합 관리하는 HUD 뷰
/// </summary>
public class PlayerHUDView : UIBase
{
    [Header("Vital Gauges - Main Sliders (HP / Stamina / Poise)")]
    [SerializeField] private Slider _hpSlider;
    [SerializeField] private RectTransform _hpFrameRect;
    [SerializeField] private Slider _staminaSlider;
    [SerializeField] private RectTransform _staminaFrameRect;
    [SerializeField] private Slider _poiseSlider;
    [SerializeField] private RectTransform _poiseFrameRect;

    [Header("Vital Gauges - Ghost Sliders (Delayed Trail)")]
    [SerializeField] private Slider _hpGhostSlider;
    [SerializeField] private Slider _staminaGhostSlider;
    [SerializeField] private Slider _poiseGhostSlider;

    [Header("Ghost Slider Animation Settings")]
    [Tooltip("게이지 감소 후 고스트 잔상이 줄어들기 시작할 때까지의 대기 시간(초)")]
    [SerializeField] private float _ghostDelayDuration = 0.45f;
    [Tooltip("고스트 잔상이 메인 게이지를 따라 감소하는 초당 비율 속도")]
    [SerializeField] private float _ghostDecaySpeed = 0.85f;

    [Header("Dynamic Gauge Width Settings (Dark Souls Ratio)")]
    [SerializeField] private float _hpWidthPerPoint = 0.65f;
    [SerializeField] private float _hpMinWidth = 160f;
    [SerializeField] private float _hpMaxWidth = 760f;

    [SerializeField] private float _staminaWidthPerPoint = 2.45f;
    [SerializeField] private float _staminaMinWidth = 120f;
    [SerializeField] private float _staminaMaxWidth = 460f;

    [SerializeField] private float _poiseWidthPerPoint = 1.35f;
    [SerializeField] private float _poiseMinWidth = 100f;
    [SerializeField] private float _poiseMaxWidth = 340f;

    [Header("Interaction Prompt (단일 안내 + 다중 선택지 통합)")]
    [SerializeField] private InteractionPromptUI _interactionPrompt;

    private GhostGaugeState _hpGhostState;
    private GhostGaugeState _staminaGhostState;
    private GhostGaugeState _poiseGhostState;

    private bool _isPromptSuppressedByUI = false;
    private bool _hasActiveSingleHint = false;
    private string _cachedSingleHintText = string.Empty;

    public InteractionPromptUI InteractionPrompt => _interactionPrompt;

    public override void InitializeView()
    {
        base.InitializeView();
        Identifier = UIId.PlayerHUD;
        Layer = UILayer.HUD;

        _hpGhostState.Reset();
        _staminaGhostState.Reset();
        _poiseGhostState.Reset();

        if (_interactionPrompt != null)
        {
            _interactionPrompt.HideAll();
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        _hpGhostState.Tick(_hpSlider, _hpGhostSlider, dt, _ghostDecaySpeed);
        _staminaGhostState.Tick(_staminaSlider, _staminaGhostSlider, dt, _ghostDecaySpeed);
        _poiseGhostState.Tick(_poiseSlider, _poiseGhostSlider, dt, _ghostDecaySpeed);
    }

    #region Vitals Update (레이아웃 크기 변경과 매 프레임 비율 갱신 완전 분리)
    /// <summary>
    /// 초기화, 장비 변경, 레벨업 등 최대 스탯 변동 이벤트(PlayerMaxStatsCalculatedEvent) 시에만 호출
    /// </summary>
    public void SetBarWidths(float maxHp, float maxStamina, float maxPoise)
    {
        ResizeBarFrame(_hpFrameRect, maxHp * _hpWidthPerPoint, _hpMinWidth, _hpMaxWidth);
        ResizeBarFrame(_staminaFrameRect, maxStamina * _staminaWidthPerPoint, _staminaMinWidth, _staminaMaxWidth);
        ResizeBarFrame(_poiseFrameRect, maxPoise * _poiseWidthPerPoint, _poiseMinWidth, _poiseMaxWidth);
    }

    public void UpdateHp(float currentHp, float maxHp)
    {
        float ratio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
        _hpGhostState.ApplyValue(_hpSlider, _hpGhostSlider, ratio, _ghostDelayDuration);
    }

    public void UpdateStamina(float currentStamina, float maxStamina)
    {
        float ratio = maxStamina > 0f ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;
        _staminaGhostState.ApplyValue(_staminaSlider, _staminaGhostSlider, ratio, _ghostDelayDuration);
    }

    public void UpdatePoise(float currentPoise, float maxPoise)
    {
        float ratio = maxPoise > 0f ? Mathf.Clamp01(currentPoise / maxPoise) : 0f;
        _poiseGhostState.ApplyValue(_poiseSlider, _poiseGhostSlider, ratio, _ghostDelayDuration);
    }

    private void ResizeBarFrame(RectTransform frameRect, float rawWidth, float minWidth, float maxWidth)
    {
        if (frameRect == null) return;
        Vector2 size = frameRect.sizeDelta;
        size.x = Mathf.Clamp(rawWidth, minWidth, maxWidth);
        frameRect.sizeDelta = size;
    }
    #endregion

    #region Interaction Prompt Control
    public void SetInteractionPromptSuppressed(bool isSuppressed)
    {
        _isPromptSuppressedByUI = isSuppressed;

        if (_interactionPrompt == null) return;

        if (_isPromptSuppressedByUI)
        {
            _interactionPrompt.HideAll();
        }
        else if (_hasActiveSingleHint && !string.IsNullOrEmpty(_cachedSingleHintText))
        {
            _interactionPrompt.ShowSingleHint(_cachedSingleHintText);
        }
    }

    public void SetInteractionPrompt(bool show, string promptText)
    {
        if (show) ShowInteractionPrompt(promptText);
        else HideInteractionPrompt();
    }

    public void ShowInteractionPrompt(string promptText)
    {
        _hasActiveSingleHint = !string.IsNullOrEmpty(promptText);
        _cachedSingleHintText = promptText ?? string.Empty;

        if (_isPromptSuppressedByUI || (UIManager.Instance != null && UIManager.Instance.HasActiveModalOrWindow()))
        {
            _interactionPrompt?.HideAll();
            return;
        }

        if (_interactionPrompt != null && _hasActiveSingleHint)
        {
            _interactionPrompt.ShowSingleHint(_cachedSingleHintText);
        }
    }

    public void HideInteractionPrompt()
    {
        _hasActiveSingleHint = false;
        _cachedSingleHintText = string.Empty;

        if (_interactionPrompt != null)
        {
            _interactionPrompt.HideAll();
        }
    }

    public void OpenInteractionChoices(IReadOnlyList<InteractionChoice> choices, Action onClosed = null)
    {
        if (_interactionPrompt != null)
        {
            _interactionPrompt.OpenChoiceMenu(choices, onClosed);
        }
    }
    #endregion

    #region Zero-GC Ghost Gauge State
    private struct GhostGaugeState
    {
        public float DelayTimer;
        public bool HasInitialized;

        public void Reset()
        {
            DelayTimer = 0f;
            HasInitialized = false;
        }

        public void ApplyValue(Slider mainSlider, Slider ghostSlider, float targetRatio, float delayDuration)
        {
            targetRatio = Mathf.Clamp01(targetRatio);

            if (!HasInitialized)
            {
                HasInitialized = true;
                DelayTimer = 0f;
                if (mainSlider != null) mainSlider.value = targetRatio;
                if (ghostSlider != null) ghostSlider.value = targetRatio;
                return;
            }

            float prevMain = mainSlider != null ? mainSlider.value : targetRatio;
            if (mainSlider != null)
            {
                mainSlider.value = targetRatio;
            }

            if (ghostSlider == null) return;

            if (targetRatio < prevMain - 0.0001f)
            {
                if (ghostSlider.value < prevMain)
                {
                    ghostSlider.value = prevMain;
                }
                DelayTimer = delayDuration;
            }
            else if (targetRatio >= ghostSlider.value)
            {
                ghostSlider.value = targetRatio;
                DelayTimer = 0f;
            }
        }

        public void Tick(Slider mainSlider, Slider ghostSlider, float deltaTime, float decaySpeed)
        {
            if (mainSlider == null || ghostSlider == null) return;

            float target = mainSlider.value;
            if (ghostSlider.value <= target)
            {
                if (ghostSlider.value < target)
                {
                    ghostSlider.value = target;
                }
                DelayTimer = 0f;
                return;
            }

            if (DelayTimer > 0f)
            {
                DelayTimer -= deltaTime;
                return;
            }

            ghostSlider.value = Mathf.MoveTowards(ghostSlider.value, target, decaySpeed * deltaTime);
        }
    }
    #endregion
}