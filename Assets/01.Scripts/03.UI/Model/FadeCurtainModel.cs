/// <summary>
/// 씬 전환 암전용 페이드 상태 데이터 모델
/// </summary>
public class FadeCurtainModel : IUIModel
{
    public float DefaultDuration { get; set; } = 0.4f;
    public bool IsFadedOut { get; set; } = false;

    public FadeCurtainModel() { }

    public FadeCurtainModel(float defaultDuration)
    {
        DefaultDuration = defaultDuration;
        IsFadedOut = false;
    }

    public void Reset()
    {
        DefaultDuration = 0.4f;
        IsFadedOut = false;
    }
}