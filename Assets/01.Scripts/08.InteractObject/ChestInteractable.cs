using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보물 상자 상호작용 컴포넌트 (BaseInteractable 기반)
/// </summary>
public class ChestInteractable : BaseInteractable
{
    [Header("Chest Settings")]
    [SerializeField] private string _promptLabel = "[E] 상자를 연다";
    [SerializeField] private ItemData _rewardItemData;
    [SerializeField] private int _rewardAmount = 1;
    [SerializeField] private Animator _chestAnimator;

    private bool _isOpened = false;

    public override string InteractionPrompt => _isOpened ? string.Empty : _promptLabel;
    protected override bool HasMultipleChoices => !_isOpened;

    protected override void BuildInteractionChoices(PlayerController player, List<InteractionChoice> outChoices)
    {
        if (_isOpened) return;

        outChoices.Add(new InteractionChoice("상자를 연다", () => OpenChest(player)));
        outChoices.Add(new InteractionChoice("그만둔다", () => { }));
    }

    private void OpenChest(PlayerController player)
    {
        if (_isOpened || player == null) return;
        _isOpened = true;

        if (_chestAnimator != null)
        {
            _chestAnimator.SetTrigger("Open");
        }

        if (_rewardItemData != null && player.TryGetComponent(out PlayerInventoryHandler invHandler))
        {
            invHandler.InventoryInstance?.AddItem(_rewardItemData, _rewardAmount);
        }

        if (UIManager.Instance != null &&
            UIManager.Instance.TryGetActiveView<PlayerHUDView>(UIId.PlayerHUD, out PlayerHUDView hudView))
        {
            hudView.HideInteractionPrompt();
        }
    }
}