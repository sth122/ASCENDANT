/// <summary>
/// 인벤토리 뷰에 투영할 데이터 스냅샷 모델
/// </summary>
public class InventoryModel : IUIModel
{
    // 좌측 슬롯 및 중량 상태
    public float CurrentWeight { get; set; } = 0f;
    public float MaxWeight { get; set; } = 40f;
    public float WeightRatio => MaxWeight > 0f ? (CurrentWeight / MaxWeight) * 100f : 0f;
    public RollTier CurrentRollTier { get; set; } = RollTier.Mid;

    // 우측 캐릭터 데이터 스냅샷
    public float CurrentHp { get; set; } = 0f;
    public float CurrentStamina { get; set; } = 0f;
    public float CurrentPoise { get; set; } = 0f;
    public float CurrentAR { get; set; } = 0f;

    // 현재 선택/포커스된 아이템
    public Item FocusedItem { get; set; } = null;

    public void Reset()
    {
        CurrentWeight = 0f;
        MaxWeight = 40f;
        CurrentRollTier = RollTier.Mid;
        CurrentHp = 0f;
        CurrentStamina = 0f;
        CurrentPoise = 0f;
        CurrentAR = 0f;
        FocusedItem = null;
    }
}