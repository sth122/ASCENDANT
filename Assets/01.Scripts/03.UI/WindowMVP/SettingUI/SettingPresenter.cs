using Cysharp.Threading.Tasks;
using UnityEngine;

public class SettingPresenter : UIPresenterBase<SettingView, SettingModel>
{
    protected override void OnBind()
    {
        if (View == null || Model == null) return;

        Model.LoadFromPrefs();
        View.SyncAllValues(Model);
        View.ShowVideoTab();

        ApplyVolumesToManager();
        OnBindHandler();
    }

    protected override void OnUnbind()
    {
        if (View == null) return;
        OnUnbindHandler();
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();

        if (View.VideoTabToggle != null)
            View.VideoTabToggle.onValueChanged.AddListener(HandleVideoTabToggled);
        if (View.AudioTabToggle != null)
            View.AudioTabToggle.onValueChanged.AddListener(HandleAudioTabToggled);

        if (View.AspectRatioDropdown != null)
            View.AspectRatioDropdown.onValueChanged.AddListener(HandleAspectRatioChanged);
        if (View.ScreenModeDropdown != null)
            View.ScreenModeDropdown.onValueChanged.AddListener(HandleScreenModeChanged);
        if (View.BackgroundSoundToggle != null)
            View.BackgroundSoundToggle.onValueChanged.AddListener(HandleBackgroundSoundChanged);

        if (View.MasterVolumeSlider != null)
            View.MasterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
        if (View.BgmVolumeSlider != null)
            View.BgmVolumeSlider.onValueChanged.AddListener(HandleBgmVolumeChanged);
        if (View.SfxVolumeSlider != null)
            View.SfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);

        if (View.TopCloseButton != null)
            View.TopCloseButton.onClick.AddListener(HandleCloseClicked);
        if (View.CloseButton != null)
            View.CloseButton.onClick.AddListener(HandleCloseClicked);
        if (View.ResetDefaultButton != null)
            View.ResetDefaultButton.onClick.AddListener(HandleResetDefaultClicked);
    }

    protected override void OnUnbindHandler()
    {
        if (View == null) return;

        if (View.VideoTabToggle != null)
            View.VideoTabToggle.onValueChanged.RemoveListener(HandleVideoTabToggled);
        if (View.AudioTabToggle != null)
            View.AudioTabToggle.onValueChanged.RemoveListener(HandleAudioTabToggled);

        if (View.AspectRatioDropdown != null)
            View.AspectRatioDropdown.onValueChanged.RemoveListener(HandleAspectRatioChanged);
        if (View.ScreenModeDropdown != null)
            View.ScreenModeDropdown.onValueChanged.RemoveListener(HandleScreenModeChanged);
        if (View.BackgroundSoundToggle != null)
            View.BackgroundSoundToggle.onValueChanged.RemoveListener(HandleBackgroundSoundChanged);

        if (View.MasterVolumeSlider != null)
            View.MasterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
        if (View.BgmVolumeSlider != null)
            View.BgmVolumeSlider.onValueChanged.RemoveListener(HandleBgmVolumeChanged);
        if (View.SfxVolumeSlider != null)
            View.SfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);

        if (View.TopCloseButton != null)
            View.TopCloseButton.onClick.RemoveListener(HandleCloseClicked);
        if (View.CloseButton != null)
            View.CloseButton.onClick.RemoveListener(HandleCloseClicked);
        if (View.ResetDefaultButton != null)
            View.ResetDefaultButton.onClick.RemoveListener(HandleResetDefaultClicked);
    }

    private void HandleVideoTabToggled(bool isOn)
    {
        if (isOn) View?.ShowVideoTab();
    }

    private void HandleAudioTabToggled(bool isOn)
    {
        if (isOn) View?.ShowAudioTab();
    }

    private void HandleAspectRatioChanged(int index)
    {
        Model.AspectRatioIndex = Mathf.Clamp(index, 0, SettingModel.AspectRatioOptions.Length - 1);
        ApplyVideoSettings();
    }

    private void HandleScreenModeChanged(int index)
    {
        Model.ScreenModeIndex = Mathf.Clamp(index, 0, SettingModel.ScreenModeLabels.Length - 1);
        ApplyVideoSettings();
    }

    private void HandleBackgroundSoundChanged(bool isOn)
    {
        Model.PlaySoundInBackground = isOn;
        Application.runInBackground = isOn;
    }

    private void HandleMasterVolumeChanged(float rawValue)
    {
        Model.MasterVolume = NormalizeSliderValue(View.MasterVolumeSlider, rawValue);
        View.SyncSliderValues(Model.MasterVolume, Model.BgmVolume, Model.SfxVolume);
        ApplyVolumesToManager();
    }

    private void HandleBgmVolumeChanged(float rawValue)
    {
        Model.BgmVolume = NormalizeSliderValue(View.BgmVolumeSlider, rawValue);
        View.SyncSliderValues(Model.MasterVolume, Model.BgmVolume, Model.SfxVolume);
        ApplyVolumesToManager();
    }

    private void HandleSfxVolumeChanged(float rawValue)
    {
        Model.SfxVolume = NormalizeSliderValue(View.SfxVolumeSlider, rawValue);
        View.SyncSliderValues(Model.MasterVolume, Model.BgmVolume, Model.SfxVolume);
        ApplyVolumesToManager();
    }

    private float NormalizeSliderValue(UnityEngine.UI.Slider slider, float rawValue)
    {
        if (slider != null && slider.maxValue > 1.5f)
        {
            return Mathf.Clamp01(rawValue / slider.maxValue);
        }
        return Mathf.Clamp01(rawValue);
    }

    private void HandleResetDefaultClicked()
    {
        Model.Reset();
        View.SyncAllValues(Model);
        ApplyVideoSettings();
        ApplyVolumesToManager();
    }

    private void HandleCloseClicked()
    {
        Model.SaveToPrefs();
        View.CloseWindowAsync().Forget();
    }

    private void ApplyVideoSettings()
    {
        var res = SettingModel.AspectRatioOptions[Model.AspectRatioIndex];
        FullScreenMode mode = Model.GetUnityFullScreenMode();
        Screen.SetResolution(res.Width, res.Height, mode);
        Application.runInBackground = Model.PlaySoundInBackground;
    }

    private void ApplyVolumesToManager()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetVolumes(Model.MasterVolume, Model.BgmVolume, Model.SfxVolume);
        }
    }
}