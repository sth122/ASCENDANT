/// <summary>
/// Player HUD의 MVP 프리젠터 클래스
/// EventBus의 체력, 스태미나, 강인도 이벤트를 수신하여 View를 동기화합니다.
/// </summary>
public class PlayerHUDPresenter : UIPresenterBase<PlayerHUDView, PlayerHUDModel>
{
    protected override void OnBind()
    {
        // EventSystem 이벤트 명시적 메서드 구독 (무명 메서드 지양 원칙 준수)
        EventBusSystem.Subscribe<UnitHpChangedEvent>(HandleHpChanged);
        EventBusSystem.Subscribe<PlayerStaminaChangedEvent>(HandleStaminaChanged);
        EventBusSystem.Subscribe<UnitPoiseChangedEvent>(HandlePoiseChanged);
        EventBusSystem.Subscribe<InteractionPromptEvent>(HandleInteractionPrompt);
        EventBusSystem.Subscribe<GameSceneReadyEvent>(HandleGameSceneReady);
        SynchronizeInitialStats();

        DebugLogger.LogWithTag("PlayerHUDPresetner", "[Presenter 바인딩 및 이벤트 구독 완료]");
    }

    protected override void OnUnbind()
    {
        EventBusSystem.Unsubscribe<UnitHpChangedEvent>(HandleHpChanged);
        EventBusSystem.Unsubscribe<PlayerStaminaChangedEvent>(HandleStaminaChanged);
        EventBusSystem.Unsubscribe<UnitPoiseChangedEvent>(HandlePoiseChanged);
        EventBusSystem.Unsubscribe<InteractionPromptEvent>(HandleInteractionPrompt);
        EventBusSystem.Unsubscribe<GameSceneReadyEvent>(HandleGameSceneReady);

        DebugLogger.LogWithTag("PlayerHUDPresetner", "[Presenter 해제 및 이벤트 구독 취소]");
    }

    #region Handle
    private void HandleGameSceneReady(GameSceneReadyEvent eventData) => SynchronizeInitialStats();

    private void HandleHpChanged(UnitHpChangedEvent eventData)
    {
        if (View == null) return;

        // 플레이어 본체 체력 변동일 때만 반영
        if (eventData.TargetUnit != null && eventData.TargetUnit.CompareTag("Player"))
        {
            if (Model != null)
            {
                Model.CurrentHp = eventData.CurrentHp;
                Model.MaxHp = eventData.MaxHp;
            }
            View.UpdateHpBar(eventData.CurrentHp, eventData.MaxHp);
        }
    }

    private void HandleStaminaChanged(PlayerStaminaChangedEvent eventData)
    {
        if (View == null) return;

        if (Model != null)
        {
            Model.CurrentStamina = eventData.CurrentStamina;
            Model.MaxStamina = eventData.MaxStamina;
        }

        View.UpdateStaminaBar(eventData.CurrentStamina, eventData.MaxStamina);
    }
    private void HandlePoiseChanged(UnitPoiseChangedEvent eventData)
    {
        if (View == null) return;

        // 플레이어 본체 강인도 변동일 때만 반영
        if (eventData.TargetUnit != null && eventData.TargetUnit.CompareTag("Player"))
        {
            if (Model != null)
            {
                Model.CurrentPoise = eventData.CurrentPoise;
                Model.MaxPoise = eventData.MaxPoise;
            }
            View.UpdatePoiseBar(eventData.CurrentPoise, eventData.MaxPoise);
        }
    }
    private void HandleInteractionPrompt(InteractionPromptEvent eventData)
    {
        if (View != null)
        {
            View.SetInteractionPrompt(eventData.Show, eventData.PromptText);
        }
    }

    #endregion

    private void SynchronizeInitialStats()
    {
        if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned && provider.Player != null)
        {
            PlayerController player = provider.Player;
            UnitStatusController stat = player.StatusController;

            if (stat != null && View != null)
            {
                float curHp = stat.CurrentHp;
                float maxHp = stat.MaxHp;
                float curPoise = stat.CurrentPoise;
                float maxPoise = stat.MaxPoise;

                float curStamina = 100f;
                float maxStamina = 100f;
                if (stat is PlayerStatusController playerStat)
                {
                    curStamina = playerStat.CurrentStamina;
                    maxStamina = playerStat.MaxStamina;
                }

                if (Model != null)
                {
                    Model.CurrentHp = curHp;
                    Model.MaxHp = maxHp;
                    Model.CurrentStamina = curStamina;
                    Model.MaxStamina = maxStamina;
                    Model.CurrentPoise = curPoise;
                    Model.MaxPoise = maxPoise;
                }

                View.UpdateHpBar(curHp, maxHp);
                View.UpdateStaminaBar(curStamina, maxStamina);
                View.UpdatePoiseBar(curPoise, maxPoise);
            }
        }
    }
}