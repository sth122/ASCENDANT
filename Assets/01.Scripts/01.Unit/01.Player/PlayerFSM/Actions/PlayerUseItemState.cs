using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 에스트 병 음용 및 투척 아이템 사용 애니메이션과 후딜레이를 제어하는 FSM 액션 상태
/// </summary>
public class PlayerUseItemState : PlayerActionState
{
    private ConsumableItem _targetItem;
    private int _targetSlotIndex = -1;
    private const float EffectTriggerNormalized = 0.50f;
    private const float CancelWindowNormalized = 0.85f;
    private bool _hasExecuted = false;
    private ItemType _itemType;

    public PlayerUseItemState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public void SetupItemToUse(int slotIndex, ConsumableItem item)
    {
        _targetSlotIndex = slotIndex;
        _targetItem = item;
    }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;
        _hasExecuted = false;
        owner.StopMovementAPI();

        if (owner.TryGetComponent(out PlayerInventoryHandler handler) && handler.InventoryInstance != null)
        {
            if (handler.InventoryInstance.QuickSlots.IsOnGlobalCooldown)
            {
                CompleteActionAndEvaluateTransition();
                return;
            }
            handler.InventoryInstance.QuickSlots.IsUsingItemLock = true;
        }

        if (_targetItem == null || _targetItem.CurrentAmount <= 0)
        {
            CompleteActionAndEvaluateTransition();
            return;
        }

        _playerAnimController.SetTrigger(_targetItem.ItemData.AnimParamType);

        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteItemUseTimelineAsync(token).Forget();
    }

    private async UniTaskVoid ExecuteItemUseTimelineAsync(System.Threading.CancellationToken token)
    {
        float duration = _playerAnimController.GetStateDuration(_playerAnimController.CurrentStateType);
        float effectDelay = duration * EffectTriggerNormalized;
        float cancelDelay = duration * CancelWindowNormalized;

        // 1. 발동 프레임 대기
        bool canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(effectDelay), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        // 2. 정식 퀵슬롯 소비 호출
        if (!_hasExecuted && owner.TryGetComponent(out PlayerInventoryHandler handler))
        {
            _hasExecuted = true;
            handler.InventoryInstance.QuickSlots.TryUseSlot(_targetSlotIndex, owner);
        }

        // 3. 후딜레이 캔슬 가능 구간 대기
        canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(cancelDelay - effectDelay), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        // 4. 구르기 선입력 캔슬 루프
        float remain = duration - cancelDelay;
        float elapsed = 0f;
        while (elapsed < remain)
        {
            if (token.IsCancellationRequested) return;

            if (owner.InputReader.TryConsumeCommand(InputCommandType.Roll))
            {
                _playerStateMachine.ChangeState(UnitState.Roll);
                return;
            }

            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
        }

        CompleteActionAndEvaluateTransition();
    }

    public override void Exit()
    {
        base.Exit();
        if (_targetItem != null && _targetItem.ItemData != null)
        {
            _playerAnimController.ResetTrigger(_targetItem.ItemData.AnimParamType);
        }

        if (owner.TryGetComponent(out PlayerInventoryHandler handler) && handler.InventoryInstance != null)
        {
            handler.InventoryInstance.QuickSlots.IsUsingItemLock = false;
        }

        _targetItem = null;
        _targetSlotIndex = -1;
        _hasExecuted = false;
    }
}