
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 그리드 및 장비 슬롯을 표현하는 단일 슬롯 UI 컴포넌트
/// </summary>
public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI Bindings")]
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _amountText;
    [SerializeField] private GameObject _equipMarker;
    [SerializeField] private GameObject _highlightOutline;

    [Header("Empty Slot & Silhouette Bindings")]
    [SerializeField] private Image _placeholderIcon; // 장비 슬롯 비어있을 때 표시되는 실루엣
    [SerializeField] private GameObject _activeFrame;    // 아이템이 있을 때만 켜지는 은색 테두리/유리광

    [Header("Drag & Drop Ghost")]
    private static GameObject _dragGhostObject;
    private static Image _dragGhostImage;
    private static ItemSlotUI _draggingSlot;

    public int QuickSlotAssignIndex { get; private set; } = -1;
    public int SlotIndex { get; private set; } = -1;             // UI 그리드 슬롯 인덱스
    public int InventorySlotIndex { get; private set; } = -1;    // 실제 Inventory._slots 배열 원본 인덱스
    public EquipSlotType? EquipmentSlotType { get; private set; } = null;
    public Item BoundItem { get; private set; }

    public event Action<ItemSlotUI, PointerEventData.InputButton> OnSlotClickedEvent;
    public event Action<ItemSlotUI, bool> OnSlotHoverEvent;

    public void InitializeSlot(int index)
    {
        SlotIndex = index;
        EquipmentSlotType = null;
        QuickSlotAssignIndex = -1;
        ClearSlot();
    }

    public void InitializeEquipSlot(EquipSlotType equipType)
    {
        SlotIndex = -1;
        EquipmentSlotType = equipType;
        QuickSlotAssignIndex = -1;
        ClearSlot();
    }

    public void InitializeQuickAssignSlot(int quickSlotIndex)
    {
        SlotIndex = -1;
        EquipmentSlotType = null;
        QuickSlotAssignIndex = quickSlotIndex;
        ClearSlot();
    }

    public void BindItem(Item item, bool isEquipped = false, int inventorySlotIndex = -1)
    {
        BoundItem = item;
        InventorySlotIndex = inventorySlotIndex >= 0 ? inventorySlotIndex : SlotIndex;

        if (item == null || item.Data == null)
        {
            ClearSlot();
            return;
        }

        if (_activeFrame != null)
        {
            _activeFrame.SetActive(true);
        }
        if (_placeholderIcon != null)
        {
            _placeholderIcon.gameObject.SetActive(false);
        }
        if (_iconImage != null)
        {
            _iconImage.gameObject.SetActive(true);
            _iconImage.sprite = item.Data.Icon;
        }

        if (_amountText != null)
        {
            if (item is CountableItem countable && countable.CurrentAmount > 1)
            {
                _amountText.gameObject.SetActive(true);
                _amountText.text = countable.CurrentAmount.ToString();
            }
            else
            {
                _amountText.gameObject.SetActive(false);
            }
        }

        if (_equipMarker != null) _equipMarker.SetActive(isEquipped);
    }

    public void ClearSlot()
    {
        BoundItem = null;
        InventorySlotIndex = -1;

        if (_activeFrame != null)
        {
            _activeFrame.SetActive(false);
        }
        if (_iconImage != null)
        {
            _iconImage.gameObject.SetActive(false);
            _iconImage.sprite = null;
        }
        if (_placeholderIcon != null)
        {
            _placeholderIcon.gameObject.SetActive(true);
        }
        if (_amountText != null)
        {
            _amountText.gameObject.SetActive(false);
        }
        if (_equipMarker != null)
        {
            _equipMarker.SetActive(false);
        }
        if (_highlightOutline != null)
        {
            _highlightOutline.SetActive(false);
        }
    }

    #region Drag & Drop 구현부
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (BoundItem == null || BoundItem.Data == null) return;
        _draggingSlot = this;

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null && _dragGhostObject == null)
        {
            _dragGhostObject = new GameObject("@DragGhostIcon", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _dragGhostObject.transform.SetParent(rootCanvas.transform, false);

            _dragGhostImage = _dragGhostObject.GetComponent<Image>();
            _dragGhostImage.raycastTarget = false;
            _dragGhostObject.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 70f);

            CanvasGroup group = _dragGhostObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0.85f;
        }

        if (_dragGhostObject != null)
        {
            _dragGhostImage.sprite = BoundItem.Data.Icon;
            _dragGhostObject.SetActive(true);
            _dragGhostObject.transform.position = eventData.position;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragGhostObject != null && _dragGhostObject.activeSelf)
        {
            _dragGhostObject.transform.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragGhostObject != null)
        {
            _dragGhostObject.SetActive(false);
        }
        _draggingSlot = null;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_draggingSlot == null || _draggingSlot.BoundItem == null) return;

        Item sourceItem = _draggingSlot.BoundItem;

        if (!ServiceLocator.TryGet<IPlayerProvider>(out var provider) || !provider.IsPlayerSpawned) return;
        PlayerController player = provider.Player;
        if (!player.TryGetComponent(out PlayerInventoryHandler handler)) return;
        Inventory inventory = handler.InventoryInstance;

        // 1. 8대 장비 슬롯 드롭 판정 (실제 InventorySlotIndex 사용)
        if (EquipmentSlotType.HasValue)
        {
            if (sourceItem is EquipmentItem equipItem)
            {
                if (equipItem.EquipData.EquipSlot != EquipmentSlotType.Value)
                {
                    DebugLogger.LogWarningWithTag("InventoryUI", $"장착 부위 불일치. 필요 부위: {EquipmentSlotType.Value}");
                    return;
                }

                PlayerInfo info = player.StatusController.PlayerInfoSource;
                if (info.Strength < equipItem.EquipData.RequiredStrength || info.Dexterity < equipItem.EquipData.RequiredDexterity)
                {
                    DebugLogger.LogWarningWithTag("InventoryUI", $"[능력치 미달] STR {equipItem.EquipData.RequiredStrength}, DEX {equipItem.EquipData.RequiredDexterity}");
                    return;
                }

                int realInvIndex = _draggingSlot.InventorySlotIndex;
                if (realInvIndex >= 0)
                {
                    inventory.EquipItem(realInvIndex, player);
                }
            }
            return;
        }

        // 2. 퀵슬롯 등록 바 드롭 판정 (가방 -> 퀵슬롯, 또는 2번 퀵슬롯 -> 3번 퀵슬롯 이동 모두 지원)
        if (QuickSlotAssignIndex >= 0)
        {
            if (sourceItem.Data is IQuickSlotUsable && sourceItem is ConsumableItem portion)
            {
                inventory.QuickSlots.RegisterItem(QuickSlotAssignIndex, portion);
            }
            return;
        }

        // 3. 장비창 -> 가방 드롭 해제 (무기/방패는 Inventory.UnequipItem 내부에서 해제 차단)
        if (_draggingSlot.EquipmentSlotType.HasValue && SlotIndex >= 0)
        {
            inventory.UnequipItem(_draggingSlot.EquipmentSlotType.Value, player);
        }
    }
    #endregion

    #region Pointer 구현부
    public void OnPointerClick(PointerEventData eventData) => OnSlotClickedEvent?.Invoke(this, eventData.button);

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_highlightOutline != null) _highlightOutline.SetActive(true);
        OnSlotHoverEvent?.Invoke(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_highlightOutline != null) _highlightOutline.SetActive(false);
        OnSlotHoverEvent?.Invoke(this, false);
    }
    #endregion
}