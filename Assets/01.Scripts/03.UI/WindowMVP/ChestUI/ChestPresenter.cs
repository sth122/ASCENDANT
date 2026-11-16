using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.EventSystems;
using ConstStrings;
using Util = DebugLogger<ChestPresenter>;

/// <summary>
/// ChestView와 ChestInteractable(상자 내부 아이템) 및 Player Inventory를 중계하는 프리젠터
/// </summary>
public class ChestPresenter : UIPresenterBase<ChestView, ChestModel>
{
    private Inventory _playerInventory;

    protected override void OnBind()
    {
        ResolvePlayerInventory();
        OnBindHandler();

        if (View != null && View.TitleText != null)
        {
            View.TitleText.text = ConstString.ChestText;
        }

        RefreshChestGrid();
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
        _playerInventory = null;
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();
        if (View == null) return;

        if (View.CloseButton != null) View.CloseButton.onClick.AddListener(HandleCloseClicked);
        if (View.TakeSelectedButton != null) View.TakeSelectedButton.onClick.AddListener(HandleTakeSelectedClicked);
        if (View.TakeAllButton != null) View.TakeAllButton.onClick.AddListener(HandleTakeAllClicked);

        for (int i = 0; i < View.GridSlots.Count; i++)
        {
            if (View.GridSlots[i] != null)
            {
                View.GridSlots[i].OnSlotClickedEvent += HandleSlotClicked;
                View.GridSlots[i].OnSlotHoverEvent += HandleSlotHover;
            }
        }
    }

    protected override void OnUnbindHandler()
    {
        if (View == null) return;

        if (View.CloseButton != null) View.CloseButton.onClick.RemoveListener(HandleCloseClicked);
        if (View.TakeSelectedButton != null) View.TakeSelectedButton.onClick.RemoveListener(HandleTakeSelectedClicked);
        if (View.TakeAllButton != null) View.TakeAllButton.onClick.RemoveListener(HandleTakeAllClicked);

        for (int i = 0; i < View.GridSlots.Count; i++)
        {
            if (View.GridSlots[i] != null)
            {
                View.GridSlots[i].OnSlotClickedEvent -= HandleSlotClicked;
                View.GridSlots[i].OnSlotHoverEvent -= HandleSlotHover;
            }
        }
    }

    private void ResolvePlayerInventory()
    {
        if (Model != null && Model.Player != null &&
            Model.Player.TryGetComponent(out PlayerInventoryHandler handler))
        {
            _playerInventory = handler.InventoryInstance;
            return;
        }

        if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned &&
            provider.Player != null && provider.Player.TryGetComponent(out PlayerInventoryHandler fallbackHandler))
        {
            Model.Player = provider.Player;
            _playerInventory = fallbackHandler.InventoryInstance;
        }
    }

    private void RefreshChestGrid()
    {
        if (View == null || Model == null || Model.SourceChest == null) return;

        IReadOnlyList<Item> items = Model.SourceChest.ChestItems;
        int itemCount = items != null ? items.Count : 0;

        for (int i = 0; i < View.GridSlots.Count; i++)
        {
            ItemSlotUI slot = View.GridSlots[i];
            if (slot == null) continue;

            if (i < itemCount && items[i] != null)
            {
                slot.BindItem(items[i], isEquipped: false, inventorySlotIndex: i);
            }
            else
            {
                slot.ClearSlot();
            }
        }

        // 선택 인덱스 보정 및 첫 아이템 자동 포커스
        if (itemCount == 0)
        {
            Model.SelectedSlotIndex = -1;
            Model.FocusedItem = null;
            View.DetailPaneUI?.ClearDetails();
            View.SetActionButtonsInteractable(canTakeSelected: false, canTakeAll: false);
            return;
        }

        if (Model.SelectedSlotIndex < 0 || Model.SelectedSlotIndex >= itemCount)
        {
            Model.SelectedSlotIndex = 0;
        }

        Model.FocusedItem = items[Model.SelectedSlotIndex];
        View.DetailPaneUI?.ShowDetails(Model.FocusedItem, _playerInventory, Model.Player);
        View.SetActionButtonsInteractable(canTakeSelected: true, canTakeAll: true);
    }

    private void HandleSlotClicked(ItemSlotUI slot, PointerEventData.InputButton button)
    {
        if (slot == null || slot.BoundItem == null || Model == null) return;

        Model.SelectedSlotIndex = slot.InventorySlotIndex;
        Model.FocusedItem = slot.BoundItem;
        View?.DetailPaneUI?.ShowDetails(slot.BoundItem, _playerInventory, Model.Player);
        View?.SetActionButtonsInteractable(canTakeSelected: true, canTakeAll: Model.SourceChest != null && Model.SourceChest.HasRemainingItems);

        // 우클릭 시 즉시 획득 편의 지원
        if (button == PointerEventData.InputButton.Right)
        {
            TryTakeItemAt(Model.SelectedSlotIndex);
        }
    }

    private void HandleSlotHover(ItemSlotUI slot, bool isEnter)
    {
        if (View?.DetailPaneUI == null || Model == null) return;

        if (isEnter && slot != null && slot.BoundItem != null)
        {
            View.DetailPaneUI.ShowDetails(slot.BoundItem, _playerInventory, Model.Player);
        }
        else if (!isEnter)
        {
            if (Model.FocusedItem != null)
                View.DetailPaneUI.ShowDetails(Model.FocusedItem, _playerInventory, Model.Player);
            else
                View.DetailPaneUI.ClearDetails();
        }
    }

    private void HandleTakeSelectedClicked()
    {
        if (Model == null || Model.SelectedSlotIndex < 0) return;
        TryTakeItemAt(Model.SelectedSlotIndex);
    }

    private void HandleTakeAllClicked()
    {
        if (Model == null || Model.SourceChest == null || _playerInventory == null) return;

        bool tookAny = false;
        while (Model.SourceChest.ChestItems.Count > 0)
        {
            Item item = Model.SourceChest.ChestItems[0];
            if (item == null || item.Data == null)
            {
                Model.SourceChest.RemoveItemAt(0, Model.Player);
                continue;
            }

            int amount = (item is CountableItem countable) ? countable.CurrentAmount : 1;
            if (!_playerInventory.AddItem(item.Data, amount))
            {
                Util.LogWarning("인벤토리가 가득 차서 일부 아이템을 획득하지 못했습니다.");
                break;
            }

            Model.SourceChest.RemoveItemAt(0, Model.Player);
            tookAny = true;
        }

        if (tookAny)
        {
            SoundManager.Instance?.PlaySFX(SoundId.UI_Confirm);
        }

        RefreshChestGrid();

        if (!Model.SourceChest.HasRemainingItems)
        {
            View?.CloseWindowAsync().Forget();
        }
    }

    private void TryTakeItemAt(int index)
    {
        if (Model == null || Model.SourceChest == null || _playerInventory == null) return;
        IReadOnlyList<Item> items = Model.SourceChest.ChestItems;
        if (index < 0 || index >= items.Count) return;

        Item targetItem = items[index];
        if (targetItem == null || targetItem.Data == null) return;

        int amount = (targetItem is CountableItem countable) ? countable.CurrentAmount : 1;
        if (!_playerInventory.AddItem(targetItem.Data, amount))
        {
            Util.LogWarning("인벤토리가 가득 찼습니다.");
            return;
        }

        SoundManager.Instance?.PlaySFX(SoundId.UI_Confirm);
        Model.SourceChest.RemoveItemAt(index, Model.Player);
        RefreshChestGrid();

        if (!Model.SourceChest.HasRemainingItems)
        {
            View?.CloseWindowAsync().Forget();
        }
    }

    private void HandleCloseClicked()
    {
        SoundManager.Instance?.PlaySFX(SoundId.UI_WindowClose);
        View?.CloseWindowAsync().Forget();
    }
}