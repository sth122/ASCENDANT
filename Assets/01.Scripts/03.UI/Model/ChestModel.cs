/// <summary>
/// 보물상자 루팅 UI 데이터 모델
/// </summary>
public class ChestModel : IUIModel
{
    public ChestInteractable SourceChest { get; set; }
    public PlayerController Player { get; set; }
    public int SelectedSlotIndex { get; set; } = -1;
    public Item FocusedItem { get; set; }

    public ChestModel() { }

    public ChestModel(ChestInteractable sourceChest, PlayerController player)
    {
        SourceChest = sourceChest;
        Player = player;
        SelectedSlotIndex = -1;
        FocusedItem = null;
    }

    public void Reset()
    {
        SourceChest = null;
        Player = null;
        SelectedSlotIndex = -1;
        FocusedItem = null;
    }
}