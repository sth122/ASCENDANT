public class PlayerHUDPresetner : UIPresenterBase<PlayerHUDView, PlayerHUDModel>
{
    protected override void OnBind()
    {
        // EventSystem 이벤트 명시적 메서드 구독 (무명 메서드 지양 원칙 준수)
        EventBusSystem.Subscribe<UnitHpChangedEvent>(HandleHpChanged);
        EventBusSystem.Subscribe<PlayerStaminaChangedEvent>(HandleStaminaChanged);

        DebugLogger.LogWithTag("PlayerHUDPresetner", "[Presenter 바인딩 및 이벤트 구독 완료]");
    }

    protected override void OnUnbind()
    {
        EventBusSystem.Unsubscribe<UnitHpChangedEvent>(HandleHpChanged);
        EventBusSystem.Unsubscribe<PlayerStaminaChangedEvent>(HandleStaminaChanged);

        DebugLogger.LogWithTag("PlayerHUDPresetner", "[Presenter 해제 및 이벤트 구독 취소]");
    }

    private void HandleHpChanged(UnitHpChangedEvent eventData)
    {
        if (View == null) return;

        // 플레이어 본체 체력 변동일 때만 반영
        if (eventData.TargetUnit != null && eventData.TargetUnit.CompareTag("Player"))
        {
            Model.CurrentHp = eventData.CurrentHp;
            Model.MaxHp = eventData.MaxHp;
            View.UpdateHpBar(eventData.CurrentHp, eventData.MaxHp);
        }
    }

    private void HandleStaminaChanged(PlayerStaminaChangedEvent eventData)
    {
        if (View == null) return;

        Model.CurrentStamina = eventData.CurrentStamina;
        Model.MaxStamina = eventData.MaxStamina;
        View.UpdateStaminaBar(eventData.CurrentStamina, eventData.MaxStamina);
    }
}