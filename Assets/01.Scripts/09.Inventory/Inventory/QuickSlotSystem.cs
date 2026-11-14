using UnityEngine;

/// <summary>
/// 소모 아이템(포션, 화염병)을 4개 퀵슬롯(1~4번 키)에 등록하고 소비하는 시스템
/// - 동일 GUID 아이템 중복 등록 불가 (기존 슬롯에서 새 슬롯으로 이동)
/// - 아이템 사용 시 전체 퀵슬롯에 공용 쿨타임 적용 (쿨타임 중 모든 아이템 사용/상태 전환 차단)
/// </summary>
public class QuickSlotSystem
{
    public const int MaxQuickSlots = 4;
    private readonly ConsumableItem[] _slots = new ConsumableItem[MaxQuickSlots];

    private float _cooldownStartTime = 0f;
    private float _cooldownEndTime = 0f;

    /// <summary>
    /// 아이템 사용 애니메이션(PlayerUseItemState) 진행 중 락
    /// </summary>
    public bool IsUsingItemLock { get; set; } = false;

    /// <summary>
    /// 공용 쿨타임 진행 여부 (true일 때 모든 퀵슬롯 아이템 사용 및 UseItem 상태 전이 불가)
    /// </summary>
    public bool IsOnGlobalCooldown => Time.time < _cooldownEndTime;

    /// <summary>
    /// 남은 쿨타임 비율 (1.0 = 막 시작, 0.0 = 종료)
    /// </summary>
    public float RemainingCooldownRatio
    {
        get
        {
            if (!IsOnGlobalCooldown) return 0f;
            float duration = _cooldownEndTime - _cooldownStartTime;
            if (duration <= 0.0001f) return 0f;
            return Mathf.Clamp01((_cooldownEndTime - Time.time) / duration);
        }
    }

    public event System.Action<int, ConsumableItem> OnQuickSlotChanged;

    public void RegisterItem(int targetSlotIndex, ConsumableItem portion)
    {
        if (targetSlotIndex < 0 || targetSlotIndex >= MaxQuickSlots) return;

        if (portion != null)
        {
            for (int i = 0; i < MaxQuickSlots; i++)
            {
                if (_slots[i] != null && _slots[i].InstanceGuid == portion.InstanceGuid)
                {
                    if (i == targetSlotIndex) return;

                    _slots[i] = null;
                    OnQuickSlotChanged?.Invoke(i, null);
                }
            }
        }

        _slots[targetSlotIndex] = portion;
        OnQuickSlotChanged?.Invoke(targetSlotIndex, portion);
    }

    public bool TryUseSlot(int slotIndex, PlayerController player)
    {
        if (slotIndex < 0 || slotIndex >= MaxQuickSlots || player == null) return false;
        if (IsOnGlobalCooldown) return false;

        ConsumableItem targetItem = _slots[slotIndex];
        if (targetItem == null || targetItem.CurrentAmount <= 0) return false;

        bool success = targetItem.Use(player);
        if (success)
        {
            float cd = Mathf.Max(0.1f, targetItem.ItemData.Cooldown);
            _cooldownStartTime = Time.time;
            _cooldownEndTime = Time.time + cd;

            OnQuickSlotChanged?.Invoke(slotIndex, targetItem);

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