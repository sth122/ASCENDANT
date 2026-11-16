/// <summary>
/// 게임오버 창 상태 데이터 모델
/// </summary>
public class GameOverModel : IUIModel
{
    public int LostSouls { get; set; } = 0;

    public void Reset()
    {
        LostSouls = 0;
    }
}