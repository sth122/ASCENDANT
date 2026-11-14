using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 열쇠 소지 여부를 검사하여 잠금을 해제하는 문/장치 상호작용 컴포넌트 (BaseInteractable 기반)
/// </summary>
public class KeyDoorInteractable : BaseInteractable
{
    [Header("Key Lock Settings")]
    [SerializeField] private string _promptLabel = "[E] 문을 연다";
    [SerializeField] private int _requiredKeyItemId;
    [SerializeField] private string _requiredKeyName = "낡은 감옥 열쇠";
    [SerializeField] private bool _consumeKeyOnUnlock = false;
    [SerializeField] private Animator _doorAnimator;

    private bool _isUnlocked = false;

    public override string InteractionPrompt => _isUnlocked ? string.Empty : _promptLabel;
    protected override bool HasMultipleChoices => !_isUnlocked;

    protected override void BuildInteractionChoices(PlayerController player, List<InteractionChoice> outChoices)
    {
        if (_isUnlocked || player == null) return;

        Inventory inventory = null;
        bool hasKey = false;

        if (player.TryGetComponent(out PlayerInventoryHandler invHandler) && invHandler.InventoryInstance != null)
        {
            inventory = invHandler.InventoryInstance;
            hasKey = inventory.HasItem(_requiredKeyItemId, 1);
        }

        if (hasKey)
        {
            outChoices.Add(new InteractionChoice($"문을 연다 ({_requiredKeyName} 사용)", () => UnlockDoor(inventory), isInteractable: true));
            outChoices.Add(new InteractionChoice("그만둔다", () => { }));
        }
        else
        {
            outChoices.Add(new InteractionChoice($"잠겨 있다 ({_requiredKeyName} 필요)", null, isInteractable: false));
            outChoices.Add(new InteractionChoice("닫기", () => { }));
        }
    }

    private void UnlockDoor(Inventory inventory)
    {
        if (_isUnlocked) return;

        if (_consumeKeyOnUnlock && inventory != null && !inventory.ConsumeItemById(_requiredKeyItemId, 1))
        {
            return;
        }

        _isUnlocked = true;

        if (_doorAnimator != null)
        {
            _doorAnimator.SetTrigger("Open");
        }

        if (UIManager.Instance != null &&
            UIManager.Instance.TryGetActiveView<PlayerHUDView>(UIId.PlayerHUD, out PlayerHUDView hudView))
        {
            hudView.HideInteractionPrompt();
        }
    }
}