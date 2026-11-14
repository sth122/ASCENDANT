using UnityEngine;

public class SettingModel : IUIModel
{
    private const string KeyAspectIndex = "Setting_AspectRatioIndex";
    private const string KeyScreenModeIndex = "Setting_ScreenModeIndex";
    private const string KeyBgSound = "Setting_BackgroundSound";

    private const string KeyMaster = "Setting_MasterVolume";
    private const string KeyBgm = "Setting_BgmVolume";
    private const string KeySfx = "Setting_SfxVolume";

    public readonly struct ResolutionOption
    {
        public readonly string Label;
        public readonly int Width;
        public readonly int Height;

        public ResolutionOption(string label, int width, int height)
        {
            Label = label;
            Width = width;
            Height = height;
        }
    }

    public static readonly ResolutionOption[] AspectRatioOptions =
    {
        new ResolutionOption("16:9  (1920 x 1080)", 1920, 1080),
        new ResolutionOption("16:9  (1600 x 900)",  1600, 900),
        new ResolutionOption("16:9  (1280 x 720)",  1280, 720),
        new ResolutionOption("16:10 (1920 x 1200)", 1920, 1200),
        new ResolutionOption("21:9  (2560 x 1080)", 2560, 1080),
        new ResolutionOption("4:3   (1280 x 960)",  1280, 960)
    };

    public static readonly string[] ScreenModeLabels =
    {
        "풀스크린 (Fullscreen)",
        "테두리 없는 창모드 (Borderless)",
        "창모드 (Windowed)"
    };

    // 비디오 설정
    public int AspectRatioIndex { get; set; } = 0;      // 기본: 16:9 (1920x1080)
    public int ScreenModeIndex { get; set; } = 0;       // 0: 풀스크린, 1: 테두리 없는 창모드, 2: 창모드
    public bool PlaySoundInBackground { get; set; } = true;

    // 오디오 설정 (0.0 ~ 1.0 정규화 값)
    public float MasterVolume { get; set; } = 1.0f;
    public float BgmVolume { get; set; } = 0.7f;
    public float SfxVolume { get; set; } = 1.0f;

    public void LoadFromPrefs()
    {
        AspectRatioIndex = Mathf.Clamp(PlayerPrefs.GetInt(KeyAspectIndex, 0), 0, AspectRatioOptions.Length - 1);
        ScreenModeIndex = Mathf.Clamp(PlayerPrefs.GetInt(KeyScreenModeIndex, 0), 0, ScreenModeLabels.Length - 1);
        PlaySoundInBackground = PlayerPrefs.GetInt(KeyBgSound, 1) == 1;

        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMaster, 1.0f));
        BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyBgm, 0.7f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfx, 1.0f));
    }

    public void SaveToPrefs()
    {
        PlayerPrefs.SetInt(KeyAspectIndex, AspectRatioIndex);
        PlayerPrefs.SetInt(KeyScreenModeIndex, ScreenModeIndex);
        PlayerPrefs.SetInt(KeyBgSound, PlaySoundInBackground ? 1 : 0);

        PlayerPrefs.SetFloat(KeyMaster, MasterVolume);
        PlayerPrefs.SetFloat(KeyBgm, BgmVolume);
        PlayerPrefs.SetFloat(KeySfx, SfxVolume);
        PlayerPrefs.Save();
    }

    public FullScreenMode GetUnityFullScreenMode()
    {
        return ScreenModeIndex switch
        {
            0 => FullScreenMode.ExclusiveFullScreen,
            1 => FullScreenMode.FullScreenWindow,
            2 => FullScreenMode.Windowed,
            _ => FullScreenMode.FullScreenWindow
        };
    }

    public static void ApplySavedSettingsOnStartup()
    {
        SettingModel bootModel = new SettingModel();
        bootModel.LoadFromPrefs();

        ResolutionOption res = AspectRatioOptions[bootModel.AspectRatioIndex];
        Screen.SetResolution(res.Width, res.Height, bootModel.GetUnityFullScreenMode());
        Application.runInBackground = bootModel.PlaySoundInBackground;
    }

    public void Reset()
    {
        AspectRatioIndex = 0;
        ScreenModeIndex = 0;
        PlaySoundInBackground = true;

        MasterVolume = 1.0f;
        BgmVolume = 0.7f;
        SfxVolume = 1.0f;
    }
}