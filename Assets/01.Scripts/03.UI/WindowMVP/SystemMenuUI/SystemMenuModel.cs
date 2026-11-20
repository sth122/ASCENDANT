/// <summary>
/// ESC 시스템 메뉴 창의 데이터 모델
/// </summary>
public class SystemMenuModel : IUIModel
{
    public float CurrentPlayTime { get; set; }

    public void Reset()
    {
        CurrentPlayTime = 0f;
    }
}