/// <summary>
/// 소모 아이템(포션, 화염병)을 슬롯에 등록하고 단축키(R키 등)로 즉각 소비하는 시스템
/// </summary>
public class QuickSlotSystem
{
    public const int MaxQuickSlots = 4;
    private readonly ConsumableItem[] _slots = new ConsumableItem[MaxQuickSlots];
    private int _selectedSlotIndex = 0;
    private float _nextAvailableUseTime = 0f;

    public int SelectedSlotIndex => _selectedSlotIndex;
    public ConsumableItem CurrentItem => _slots[_selectedSlotIndex];

    public event System.Action<int, ConsumableItem> OnQuickSlotChanged;
    public event System.Action<int> OnSelectedSlotChanged;

    public void RegisterItem(int slotIndex, ConsumableItem portion)
    {
        if (slotIndex < 0 || slotIndex >= MaxQuickSlots) return;

        _slots[slotIndex] = portion;
        OnQuickSlotChanged?.Invoke(slotIndex, portion);
    }

    public void SelectNextSlot()
    {
        _selectedSlotIndex = (_selectedSlotIndex + 1) % MaxQuickSlots;
        OnSelectedSlotChanged?.Invoke(_selectedSlotIndex);
    }

    public bool TryUseCurrentItem(PlayerController player)
    {
        ConsumableItem current = CurrentItem;
        if (current == null || current.CurrentAmount <= 0) return false;
        if (UnityEngine.Time.time < _nextAvailableUseTime) return false;

        bool success = current.Use(player);
        if (success)
        {
            _nextAvailableUseTime = UnityEngine.Time.time + current.ItemData.Cooldown;
            OnQuickSlotChanged?.Invoke(_selectedSlotIndex, current);

            if (current.CurrentAmount <= 0)
            {
                _slots[_selectedSlotIndex] = null;
                OnQuickSlotChanged?.Invoke(_selectedSlotIndex, null);
            }
        }
        return success;
    }

    /// <summary>
    /// 지정된 인덱스(0~3)의 퀵슬롯 아이템을 즉각 소비
    /// </summary>
    /// <param name="slotIndex">0부터 시작하는 퀵슬롯 번호</param>
    /// <param name="player">사용 주체 플레이어</param>
    public bool TryUseSlot(int slotIndex, PlayerController player)
    {
        if (slotIndex < 0 || slotIndex >= MaxQuickSlots || player == null) return false;

        ConsumableItem targetItem = _slots[slotIndex];
        if (targetItem == null || targetItem.CurrentAmount <= 0) return false;
        if (UnityEngine.Time.time < _nextAvailableUseTime) return false;

        bool success = targetItem.Use(player);
        if (success)
        {
            _nextAvailableUseTime = UnityEngine.Time.time + targetItem.ItemData.Cooldown;
            OnQuickSlotChanged?.Invoke(slotIndex, targetItem);

            // 수량 소진 시 퀵슬롯 비우기 및 인벤토리 가방 슬롯 연계 소거
            if (targetItem.CurrentAmount <= 0)
            {
                _slots[slotIndex] = null;
                OnQuickSlotChanged?.Invoke(slotIndex, null);

                if (player.TryGetComponent(out PlayerInventoryHandler handler) && handler.InventoryInstance != null)
                {
                    handler.InventoryInstance.CleanDepletedSlot(targetItem);
                }
            }
        }
        return success;
    }

    public Item GetItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MaxQuickSlots) return null;
        return _slots[slotIndex];
    }
}