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

        DebugLogger.LogWithTag("PlayerHUDPresetner", "[Presenter 바인딩 및 이벤트 구독 완료]");
    }

    protected override void OnUnbind()
    {
        EventBusSystem.Unsubscribe<UnitHpChangedEvent>(HandleHpChanged);
        EventBusSystem.Unsubscribe<PlayerStaminaChangedEvent>(HandleStaminaChanged);
        EventBusSystem.Unsubscribe<UnitPoiseChangedEvent>(HandlePoiseChanged);

        DebugLogger.LogWithTag("PlayerHUDPresetner", "[Presenter 해제 및 이벤트 구독 취소]");
    }

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
}