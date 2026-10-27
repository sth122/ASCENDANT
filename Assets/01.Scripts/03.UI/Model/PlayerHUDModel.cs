/// <summary>
/// Player HUD에 바인딩되는 데이터 모델 클래스
/// 체력, 스태미나, 강인도 수치를 보관
/// </summary>
public class PlayerHUDModel : IUIModel
{
    public float CurrentHp { get; set; }
    public float MaxHp { get; set; }
    public float CurrentStamina { get; set; }
    public float MaxStamina { get; set; }
    public float CurrentPoise { get; set; }
    public float MaxPoise { get; set; }

    public void Reset()
    {
        CurrentHp = 0f;
        MaxHp = 0f;
        CurrentStamina = 0f;
        MaxStamina = 0f;
        CurrentPoise = 0f;
        MaxPoise = 0f;
    }
}