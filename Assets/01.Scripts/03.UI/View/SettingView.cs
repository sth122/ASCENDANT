using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingView : UIWindowView
{
    [Header("Category Tabs (비디오 / 오디오)")]
    [SerializeField] private Toggle _videoTabToggle;
    [SerializeField] private Toggle _audioTabToggle;
    [SerializeField] private GameObject _videoPanel;
    [SerializeField] private GameObject _audioPanel;

    [Header("Video Controls (화면 비율 / 화면 모드 / 백그라운드 사운드)")]
    [SerializeField] private TMP_Dropdown _aspectRatioDropdown;
    [SerializeField] private TMP_Dropdown _screenModeDropdown;
    [SerializeField] private Toggle _backgroundSoundToggle;

    [Header("Audio Sliders (마스터 / BGM / 효과음)")]
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private TextMeshProUGUI _masterValueText;

    [SerializeField] private Slider _bgmVolumeSlider;
    [SerializeField] private TextMeshProUGUI _bgmValueText;

    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI _sfxValueText;

    [Header("Action Buttons")]
    [SerializeField] private Button _topCloseButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _resetDefaultButton;

    public Toggle VideoTabToggle => _videoTabToggle;
    public Toggle AudioTabToggle => _audioTabToggle;

    public TMP_Dropdown AspectRatioDropdown => _aspectRatioDropdown;
    public TMP_Dropdown ScreenModeDropdown => _screenModeDropdown;
    public Toggle BackgroundSoundToggle => _backgroundSoundToggle;

    public Slider MasterVolumeSlider => _masterVolumeSlider;
    public Slider BgmVolumeSlider => _bgmVolumeSlider;
    public Slider SfxVolumeSlider => _sfxVolumeSlider;

    public Button TopCloseButton => _topCloseButton;
    public Button CloseButton => _closeButton;
    public Button ResetDefaultButton => _resetDefaultButton;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.Window;
        _pausePolicy = WindowPausePolicy.PauseTime;

        PopulateDropdownOptions();
        ShowVideoTab();
    }

    public override async Cysharp.Threading.Tasks.UniTask ShowAsync(float duration = 0.1f, System.Threading.CancellationToken ct = default)
    {
        EventBusSystem.Publish(new TitleIntroVisibilityEvent(false));
        await base.ShowAsync(duration, ct);
    }

    public override async Cysharp.Threading.Tasks.UniTask CloseWindowAsync(float duration = 0.15f)
    {
        await base.CloseWindowAsync(duration);
        EventBusSystem.Publish(new TitleIntroVisibilityEvent(true));
    }

    private void PopulateDropdownOptions()
    {
        if (_aspectRatioDropdown != null)
        {
            _aspectRatioDropdown.ClearOptions();
            List<string> ratioLabels = new List<string>(SettingModel.AspectRatioOptions.Length);
            for (int i = 0; i < SettingModel.AspectRatioOptions.Length; i++)
            {
                ratioLabels.Add(SettingModel.AspectRatioOptions[i].Label);
            }
            _aspectRatioDropdown.AddOptions(ratioLabels);
        }

        if (_screenModeDropdown != null)
        {
            _screenModeDropdown.ClearOptions();
            _screenModeDropdown.AddOptions(new List<string>(SettingModel.ScreenModeLabels));
        }
    }

    public void ShowVideoTab()
    {
        if (_videoPanel != null) _videoPanel.SetActive(true);
        if (_audioPanel != null) _audioPanel.SetActive(false);

        if (_videoTabToggle != null) _videoTabToggle.SetIsOnWithoutNotify(true);
        if (_audioTabToggle != null) _audioTabToggle.SetIsOnWithoutNotify(false);
    }

    public void ShowAudioTab()
    {
        if (_videoPanel != null) _videoPanel.SetActive(false);
        if (_audioPanel != null) _audioPanel.SetActive(true);

        if (_videoTabToggle != null) _videoTabToggle.SetIsOnWithoutNotify(false);
        if (_audioTabToggle != null) _audioTabToggle.SetIsOnWithoutNotify(true);
    }

    public void SyncAllValues(SettingModel model)
    {
        if (model == null) return;

        if (_aspectRatioDropdown != null)
            _aspectRatioDropdown.SetValueWithoutNotify(model.AspectRatioIndex);
        if (_screenModeDropdown != null)
            _screenModeDropdown.SetValueWithoutNotify(model.ScreenModeIndex);
        if (_backgroundSoundToggle != null)
            _backgroundSoundToggle.SetIsOnWithoutNotify(model.PlaySoundInBackground);

        SyncSliderValues(model.MasterVolume, model.BgmVolume, model.SfxVolume);
    }

    public void SyncSliderValues(float master, float bgm, float sfx)
    {
        SetSliderNormalized(_masterVolumeSlider, _masterValueText, master);
        SetSliderNormalized(_bgmVolumeSlider, _bgmValueText, bgm);
        SetSliderNormalized(_sfxVolumeSlider, _sfxValueText, sfx);
    }

    private void SetSliderNormalized(Slider slider, TextMeshProUGUI valueText, float normalized)
    {
        float clamped = Mathf.Clamp01(normalized);
        if (slider != null)
        {
            float targetVal = slider.maxValue > 1.5f ? Mathf.Round(clamped * 100f) : clamped;
            slider.SetValueWithoutNotify(targetVal);
        }
        if (valueText != null)
        {
            valueText.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
        }
    }
}