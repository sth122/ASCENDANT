
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

    [Header("Drag & Drop Ghost")]
    private static GameObject _dragGhostObject;
    private static Image _dragGhostImage;
    private static ItemSlotUI _draggingSlot;

    public int QuickSlotAssignIndex { get; private set; } = -1;
    public int SlotIndex { get; private set; } = -1;
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

    #region Drag & Drop 구현부
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (BoundItem == null || BoundItem.Data == null) return;

        _draggingSlot = this;

        // 드래그 고스트 생성 및 설정
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null && _dragGhostObject == null)
        {
            _dragGhostObject = new GameObject("@DragGhostIcon", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _dragGhostObject.transform.SetParent(rootCanvas.transform, false);

            _dragGhostImage = _dragGhostObject.GetComponent<Image>();
            _dragGhostImage.raycastTarget = false;

            RectTransform rect = _dragGhostObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(70f, 70f);

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

        // 1. [하단 좌측 6대 장비 슬롯 드롭 판정]
        if (EquipmentSlotType.HasValue)
        {
            if (sourceItem is EquipmentItem equipItem)
            {
                // 장비 슬롯 부위 일치 검증
                if (equipItem.EquipData.EquipSlot != EquipmentSlotType.Value)
                {
                    DebugLogger.LogWarningWithTag("InventoryUI", $"장착 부위가 일치하지 않습니다. 필요 부위: {EquipmentSlotType.Value}");
                    return;
                }

                // 캐릭터 스탯(STR, DEX) 요구치 검증
                PlayerInfo info = player.StatusController.PlayerInfoSource;
                if (info.Strength < equipItem.EquipData.RequiredStrength || info.Dexterity < equipItem.EquipData.RequiredDexterity)
                {
                    DebugLogger.LogWarningWithTag("InventoryUI", $"[장착 불가] 능력치 미달! 요구 근력: {equipItem.EquipData.RequiredStrength}, 요구 기량: {equipItem.EquipData.RequiredDexterity}");
                    return;
                }

                // 장착 실행
                if (_draggingSlot.SlotIndex >= 0)
                {
                    inventory.EquipItem(_draggingSlot.SlotIndex, player);
                }
            }
            return;
        }

        // 2. [최하단 좌측 퀵슬롯 등록 바 드롭 판정]
        if (QuickSlotAssignIndex >= 0)
        {
            // IQuickSlotUsable 인터페이스 기반 검증 (일반 장비/재료 원천 차단)
            if (sourceItem.Data is IQuickSlotUsable && sourceItem is PortionItem portion)
            {
                inventory.QuickSlots.RegisterItem(QuickSlotAssignIndex, portion);
                DebugLogger.LogWithTag("InventoryUI", $"{QuickSlotAssignIndex}번 퀵슬롯에 [{portion.Data.ItemName}] 등록 완료");
            }
            else
            {
                DebugLogger.LogWarningWithTag("InventoryUI", "퀵슬롯에 등록할 수 없는 아이템입니다. (IQuickSlotUsable 미구현)");
            }
            return;
        }

        // 3. [장비 슬롯 -> 가방 그리드로 드롭하여 장착 해제]
        if (_draggingSlot.EquipmentSlotType.HasValue && SlotIndex >= 0)
        {
            inventory.UnequipItem(_draggingSlot.EquipmentSlotType.Value, player);
        }
    }
    #endregion

    public void BindItem(Item item, bool isEquipped = false)
    {
        BoundItem = item;

        if (item == null || item.Data == null)
        {
            ClearSlot();
            return;
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

        if (_equipMarker != null)
        {
            _equipMarker.SetActive(isEquipped);
        }
    }

    public void ClearSlot()
    {
        BoundItem = null;

        if (_iconImage != null)
        {
            _iconImage.gameObject.SetActive(false);
            _iconImage.sprite = null;
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

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSlotClickedEvent?.Invoke(this, eventData.button);
    }

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
}