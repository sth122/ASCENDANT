/// <summary>
/// Player HUD의 MVP 프리젠터 클래스
/// EventBus의 체력, 스태미나, 강인도, 상호작용 프롬프트 이벤트를 수신하여 View를 동기화합니다.
/// </summary>
public class PlayerHUDPresenter : UIPresenterBase<PlayerHUDView, PlayerHUDModel>
{
    private PlayerController _cachedPlayer;

    protected override void OnBind()
    {
        ResolvePlayerReference();

        EventBusSystem.Subscribe<UnitHpChangedEvent>(HandleHpChanged);
        EventBusSystem.Subscribe<PlayerStaminaChangedEvent>(HandleStaminaChanged);
        EventBusSystem.Subscribe<UnitPoiseChangedEvent>(HandlePoiseChanged);
        EventBusSystem.Subscribe<PlayerMaxStatsCalculatedEvent>(HandleMaxStatsCalculated);
        EventBusSystem.Subscribe<InteractionPromptEvent>(HandleInteractionPrompt);
        EventBusSystem.Subscribe<GameSceneReadyEvent>(HandleGameSceneReady);

        SynchronizeInitialStats();
        DebugLogger.LogWithTag("PlayerHUDPresenter", "[Presenter 바인딩 및 이벤트 구독 완료]");
    }

    protected override void OnUnbind()
    {
        EventBusSystem.Unsubscribe<UnitHpChangedEvent>(HandleHpChanged);
        EventBusSystem.Unsubscribe<PlayerStaminaChangedEvent>(HandleStaminaChanged);
        EventBusSystem.Unsubscribe<UnitPoiseChangedEvent>(HandlePoiseChanged);
        EventBusSystem.Unsubscribe<PlayerMaxStatsCalculatedEvent>(HandleMaxStatsCalculated);
        EventBusSystem.Unsubscribe<InteractionPromptEvent>(HandleInteractionPrompt);
        EventBusSystem.Unsubscribe<GameSceneReadyEvent>(HandleGameSceneReady);

        _cachedPlayer = null;
        DebugLogger.LogWithTag("PlayerHUDPresenter", "[Presenter 해제 및 이벤트 구독 취소]");
    }

    private void ResolvePlayerReference()
    {
        if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned && provider.Player != null)
        {
            _cachedPlayer = provider.Player;
        }
    }

    #region Event Handlers
    private void HandleGameSceneReady(GameSceneReadyEvent eventData)
    {
        ResolvePlayerReference();
        SynchronizeInitialStats();
    }

    private void HandleHpChanged(UnitHpChangedEvent eventData)
    {
        if (View == null) return;

        if (eventData.TargetUnit != null && eventData.TargetUnit.CompareTag("Player"))
        {
            if (Model != null)
            {
                Model.CurrentHp = eventData.CurrentHp;
                Model.MaxHp = eventData.MaxHp;
            }
            View.UpdateHp(eventData.CurrentHp, eventData.MaxHp);
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
        View.UpdateStamina(eventData.CurrentStamina, eventData.MaxStamina);
    }

    private void HandleMaxStatsCalculated(PlayerMaxStatsCalculatedEvent eventData)
    {
        if (View == null) return;

        if (Model != null)
        {
            Model.MaxHp = eventData.MaxHp;
            Model.MaxStamina = eventData.MaxStamina;
            Model.MaxPoise = eventData.MaxPoise;
        }
        View.SetBarWidths(eventData.MaxHp, eventData.MaxStamina, eventData.MaxPoise);
    }

    private void HandlePoiseChanged(UnitPoiseChangedEvent eventData)
    {
        if (View == null || eventData.TargetUnit == null) return;

        if (eventData.TargetUnit.TryGetComponent<PlayerController>(out _))
        {
            if (Model != null)
            {
                Model.CurrentPoise = eventData.CurrentPoise;
                Model.MaxPoise = eventData.MaxPoise;
            }
            View.UpdatePoise(eventData.CurrentPoise, eventData.MaxPoise);
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
        if (_cachedPlayer == null) ResolvePlayerReference();
        if (_cachedPlayer == null || _cachedPlayer.StatusController == null || View == null) return;

        UnitStatusController stat = _cachedPlayer.StatusController;

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

        View.SetBarWidths(maxHp, maxStamina, maxPoise);
        View.UpdateHp(curHp, maxHp);
        View.UpdateStamina(curStamina, maxStamina);
        View.UpdatePoise(curPoise, maxPoise);
    }
}