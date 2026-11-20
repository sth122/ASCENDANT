/// <summary>
/// 화면 하단 보스 HUD에 바인딩되는 데이터 모델
/// </summary>
public class BossHUDModel : IUIModel
{
    public string BossName { get; set; } = string.Empty;
    public UnityEngine.Sprite PortraitSprite { get; set; }
    public float CurrentHp { get; set; }
    public float MaxHp { get; set; }
    public float CurrentPoise { get; set; }
    public float MaxPoise { get; set; }
    public bool IsActive { get; set; } = false;

    public void Reset()
    {
        BossName = string.Empty;
        PortraitSprite = null;
        CurrentHp = 0f;
        MaxHp = 0f;
        CurrentPoise = 0f;
        MaxPoise = 0f;
        IsActive = false;
    }
}