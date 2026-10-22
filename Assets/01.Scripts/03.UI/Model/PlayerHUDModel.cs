public class PlayerHUDModel : IUIModel
{
    public float CurrentHp { get; set; }
    public float MaxHp { get; set; }
    public float CurrentStamina { get; set; }
    public float MaxStamina { get; set; }

    public void Reset()
    {
        CurrentHp = 0f;
        MaxHp = 0f;
        CurrentStamina = 0f;
        MaxStamina = 0f;
    }
}