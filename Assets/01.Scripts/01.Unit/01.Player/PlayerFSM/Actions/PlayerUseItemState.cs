using Cysharp.Threading.Tasks;

/// <summary>
/// 에스트 병 음용 및 투척 아이템 사용 애니메이션과 후딜레이를 제어하는 FSM 액션 상태
/// </summary>
public class PlayerUseItemState : PlayerActionState
{
    private PortionItem _targetItem;
    private int _targetSlotIndex;
    private const float ItemEffectTriggerNormalized = 0.50f; // 포션 음용/투척 발동 정규화 시점
    private const float MovementCancelNormalized = 0.85f;   // 후딜레이 캔슬 가능 시점
    private bool _hasExecutedEffect = false;

    public PlayerUseItemState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public void SetupItemToUse(int slotIndex, PortionItem item)
    {
        _targetSlotIndex = slotIndex;
        _targetItem = item;
    }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;
        _hasExecutedEffect = false;
        owner.StopMovementAPI();

        if (_targetItem == null || _targetItem.CurrentAmount <= 0)
        {
            CompleteActionAndEvaluateTransition();
            return;
        }

        // 아이템 사용 애니메이션 트리거 격발 (투척/음용 구분)
        // _playerAnimController.SetTrigger(AnimParamType.UseItem);

        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteItemUseTimelineAsync(token).Forget();
    }

    private async UniTaskVoid ExecuteItemUseTimelineAsync(System.Threading.CancellationToken token)
    {
        float totalDuration = 1.6f; // 소울라이크 에스트 음용 평균 모션 시간
        float effectTime = totalDuration * ItemEffectTriggerNormalized;
        float cancelWindowTime = totalDuration * MovementCancelNormalized;

        // 1. 발동 시점까지 대기 (선딜레이)
        bool canceledBeforeEffect = await UniTask.Delay(System.TimeSpan.FromSeconds(effectTime), cancellationToken: token).SuppressCancellationThrow();
        if (canceledBeforeEffect) return;

        // 2. 실제 아이템 효과 적용 및 퀵슬롯 차감
        if (!_hasExecutedEffect && _targetItem != null && _targetItem.CurrentAmount > 0)
        {
            _hasExecutedEffect = true;
            _targetItem.Use(owner);
        }

        // 3. 후딜레이 캔슬 가능 시점까지 대기
        float remainToCancel = cancelWindowTime - effectTime;
        bool canceledBeforeCancelWindow = await UniTask.Delay(System.TimeSpan.FromSeconds(remainToCancel), cancellationToken: token).SuppressCancellationThrow();
        if (canceledBeforeCancelWindow) return;

        // 4. 후딜레이 구간: 구르기 선입력 시 즉시 회피 캔슬 허용
        float remainingDuration = totalDuration - cancelWindowTime;
        float elapsed = 0f;

        while (elapsed < remainingDuration)
        {
            if (token.IsCancellationRequested) return;

            if (owner.InputReader.TryConsumeCommand(InputCommandType.Roll))
            {
                _playerStateMachine.ChangeState(UnitState.Roll);
                return;
            }

            elapsed += UnityEngine.Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
        }

        CompleteActionAndEvaluateTransition();
    }

    public override void Exit()
    {
        base.Exit();
        _targetItem = null;
        _hasExecutedEffect = false;
    }
}