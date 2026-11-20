using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 좌측 Bag_ScrollView(상자 아이템 슬롯) + 우측 Center_Column(ItemDetailPaneUI) 구성의 보물상자 뷰
/// </summary>
public class ChestView : UIWindowView
{
    [Header("Top Header Bar")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private Button _closeButton;

    [Header("Left: Chest Bag ScrollView Slots")]
    [SerializeField] private List<ItemSlotUI> _gridSlots = new List<ItemSlotUI>(Inventory.DefaultCapacity);

    [Header("Right: Item Detail Panel (Center_Column 재사용)")]
    [SerializeField] private ItemDetailPaneUI _detailPaneUI;

    [Header("Action Buttons")]
    [SerializeField] private Button _takeSelectedButton;
    [SerializeField] private Button _takeAllButton;

    public TextMeshProUGUI TitleText => _titleText;
    public Button CloseButton => _closeButton;
    public IReadOnlyList<ItemSlotUI> GridSlots => _gridSlots;
    public ItemDetailPaneUI DetailPaneUI => _detailPaneUI;
    public Button TakeSelectedButton => _takeSelectedButton;
    public Button TakeAllButton => _takeAllButton;

    public override void InitializeView()
    {
        base.InitializeView();
        Identifier = UIId.ChestUI;
        Layer = UILayer.Window;
        _pausePolicy = WindowPausePolicy.PauseTime;

        for (int i = 0; i < _gridSlots.Count; i++)
        {
            if (_gridSlots[i] != null)
            {
                _gridSlots[i].InitializeSlot(i);
            }
        }
    }

    public void SetActionButtonsInteractable(bool canTakeSelected, bool canTakeAll)
    {
        if (_takeSelectedButton != null) _takeSelectedButton.interactable = canTakeSelected;
        if (_takeAllButton != null) _takeAllButton.interactable = canTakeAll;
    }
}