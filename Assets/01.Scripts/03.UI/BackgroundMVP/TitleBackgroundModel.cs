public class TitleBackgroundModel : IUIModel
{
    public float BreathZoomAmplitude { get; set; } = 0.03f;
    public float BreathZoomSpeed { get; set; } = 0.35f;

    public void Reset()
    {
        BreathZoomAmplitude = 0.03f;
        BreathZoomSpeed = 0.35f;
    }
}