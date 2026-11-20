/// <summary>
/// 에너미 머리 위 플로팅 HUD의 상태를 보존하는 전용 데이터 모델
/// </summary>
public class EnemyFloatingHUDModel : IUIModel
{
    public float CurrentHp { get; set; }
    public float MaxHp { get; set; }
    public float CurrentPoise { get; set; }
    public float MaxPoise { get; set; }
    public bool IsGroggy { get; set; }
    public bool CanBeRiposted { get; set; }

    public float HpRatio => MaxHp > 0f ? UnityEngine.Mathf.Clamp01(CurrentHp / MaxHp) : 1f;
    public float PoiseRatio => MaxPoise > 0f ? UnityEngine.Mathf.Clamp01(CurrentPoise / MaxPoise) : 0f;

    public void Reset()
    {
        CurrentHp = 0f;
        MaxHp = 1f;
        CurrentPoise = 0f;
        MaxPoise = 1f;
        IsGroggy = false;
        CanBeRiposted = false;
    }
}