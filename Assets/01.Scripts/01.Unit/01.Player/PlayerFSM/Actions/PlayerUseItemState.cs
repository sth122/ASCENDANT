using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 에스트 병 음용 및 투척 아이템 사용 애니메이션과 후딜레이를 제어하는 FSM 액션 상태
/// </summary>
public class PlayerUseItemState : PlayerActionState
{
    private ConsumableItem _targetItem;
    private int _targetSlotIndex = -1;
    private const float EffectTriggerNormalized = 0.50f;
    private const float CancelWindowNormalized = 2f;
    private bool _hasExecuted = false;

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

        // 락온 타깃이 존재하면 투척/사용 시작 시 대상 방향으로 정렬
        if (owner.CurrentLockOnTarget != null)
        {
            owner.Movement.SnapToTarget(owner.CurrentLockOnTarget.position);
        }
        
        AnimParamType triggerParam = _targetItem.ItemData.AnimParamType;
        _playerAnimController.SetTrigger(triggerParam);

        System.Threading.CancellationToken token = CreateActionToken(); 
        ExecuteItemUseTimelineAsync(triggerParam, token).Forget();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // 아이템 사용 모션 중에는 이동 물리 속도 완전 고정
        if (!CanMove)
        {
            owner.StopMovementAPI();
        }
    }

    private async UniTaskVoid ExecuteItemUseTimelineAsync(AnimParamType triggerParam, CancellationToken token)
    {
        string expectedStateName = ResolveAnimatorStateName(triggerParam);
        float enterElapsed = 0f;
        float clipDuration = 0f;

        if (_playerAnimController.AnimatorInstance != null)
        {
            // 1. Animator가 해당 아이템 애니메이션 상태로 전이 완료될 때까지 대기 (최대 0.3초)
            while (enterElapsed < 0.30f)
            {
                if (token.IsCancellationRequested) return;

                AnimatorStateInfo info = _playerAnimController.AnimatorInstance.GetCurrentAnimatorStateInfo(0);
                if (!_playerAnimController.AnimatorInstance.IsInTransition(0) && info.IsName(expectedStateName) && info.length > 0.05f)
                {
                    float speed = Mathf.Max(0.1f, Mathf.Abs(info.speed * info.speedMultiplier));
                    clipDuration = info.length / speed;
                    break;
                }

                enterElapsed += Time.deltaTime;
                bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (canceled) return;
            }

            // 폴백: 상태명 매칭 전이라면 RuntimeAnimatorController 클립 목록에서 직접 조회
            if (clipDuration <= 0.05f && _playerAnimController.AnimatorInstance.runtimeAnimatorController != null)
            {
                AnimationClip[] clips = _playerAnimController.AnimatorInstance.runtimeAnimatorController.animationClips;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] != null && clips[i].name.IndexOf(expectedStateName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        clipDuration = clips[i].length;
                        break;
                    }
                }
            }
        }

        if (clipDuration <= 0.05f)
        {
            clipDuration = 1.5f;
        }

        float effectDelay = Mathf.Max(0.05f, (clipDuration * EffectTriggerNormalized) - enterElapsed);
        float cancelPoint = clipDuration * CancelWindowNormalized;
        float postEffectDelay = Mathf.Max(0.05f, cancelPoint - (enterElapsed + effectDelay));

        // 2. 아이템 효과 발동 프레임까지 대기
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(effectDelay), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // 3. 정식 퀵슬롯 아이템 소비 및 효과 발동
        if (!_hasExecuted && owner.TryGetComponent(out PlayerInventoryHandler handler) && handler.InventoryInstance != null)
        {
            _hasExecuted = true;
            handler.InventoryInstance.QuickSlots.TryUseSlot(_targetSlotIndex, owner);
        }

        // 4. 후딜레이 종료 직전까지 이동 잠금 유지
        isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(postEffectDelay), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // 5. 모션 마무리 구간 (구르기 긴급 캔슬만 허용, 일반 이동은 모션 완전 종료 시까지 잠금 유지)
        float remain = Mathf.Max(0f, clipDuration - cancelPoint);
        float elapsed = 0f;
        while (elapsed < remain)
        {
            if (token.IsCancellationRequested) return;

            if (owner.InputReader.TryConsumeCommand(InputCommandType.Roll))
            {
                float rollCost = _playerCombatConfigSO != null ? _playerCombatConfigSO.RollStaminaCost : 18.0f;
                if (_playerStatusController != null && _playerStatusController.HasEnoughStamina(rollCost))
                {
                    _playerStateMachine.ChangeState(UnitState.Roll, true);
                    return;
                }
                owner.InputReader.ClearCommand(InputCommandType.Roll);
            }

            elapsed += Time.deltaTime;
            bool yieldCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (yieldCanceled) return;
        }

        CompleteActionAndEvaluateTransition();
    }

    private static string ResolveAnimatorStateName(AnimParamType paramType)
    {
        return paramType switch
        {
            AnimParamType.UsePortion => "UsePotion",
            AnimParamType.ThrowItem => "ThrowBomb",
            _ => paramType.ToString()
        };
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