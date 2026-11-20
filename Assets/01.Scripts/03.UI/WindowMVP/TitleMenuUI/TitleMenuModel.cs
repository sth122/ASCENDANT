/// <summary>
/// 타이틀 메뉴 데이터 모델
/// </summary>
public class TitleMenuModel : IUIModel
{
    public bool CanContinueGame { get; set; } = false;

    public void RefreshSaveState()
    {
        CanContinueGame = SaveManager.Instance != null && SaveManager.Instance.HasSaveData(0);
    }

    public void Reset()
    {
        CanContinueGame = false;
    }
}