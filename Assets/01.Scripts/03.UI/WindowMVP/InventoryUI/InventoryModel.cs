/// <summary>
/// 인벤토리 뷰에 투영할 데이터 스냅샷 모델
/// </summary>
public class InventoryModel : IUIModel
{
    public int SoulLevel { get; set; }
    public int CurrentSouls { get; set; }

    public int Vigor { get; set; }
    public int Endurance { get; set; }
    public int Strength { get; set; }
    public int Dexterity { get; set; }

    public int BonusStrength { get; set; }
    public int BonusDexterity { get; set; }

    public float CurrentHp { get; set; }
    public float MaxHp { get; set; }
    public float CurrentStamina { get; set; }
    public float MaxStamina { get; set; }

    public float CurrentWeight { get; set; }
    public float MaxWeight { get; set; }
    public RollTier CurrentRollTier { get; set; }

    public float CurrentAR { get; set; }
    public float CurrentPoise { get; set; }
    public float FlatDefense { get; set; }
    public float Absorption { get; set; }

    public Item FocusedItem { get; set; }

    public void Reset()
    {
        FocusedItem = null;
    }
}