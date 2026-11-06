using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// BossHUDView와 BossHUDModel을 중계하는 MVP Presenter
/// </summary>
public class BossHUDPresenter : UIPresenterBase<BossHUDView, BossHUDModel>
{
    private GameObject _boundBossObject;

    protected override void OnBind()
    {
        OnBindHandler();
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
        _boundBossObject = null;
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();
        EventBusSystem.Subscribe<BossEncounterEvent>(HandleBossEncounter);
        EventBusSystem.Subscribe<BossDefeatedEvent>(HandleBossDefeated);
        EventBusSystem.Subscribe<UnitHpChangedEvent>(HandleHpChanged);
        EventBusSystem.Subscribe<UnitPoiseChangedEvent>(HandlePoiseChanged);
    }

    protected override void OnUnbindHandler()
    {
        EventBusSystem.Unsubscribe<BossEncounterEvent>(HandleBossEncounter);
        EventBusSystem.Unsubscribe<BossDefeatedEvent>(HandleBossDefeated);
        EventBusSystem.Unsubscribe<UnitHpChangedEvent>(HandleHpChanged);
        EventBusSystem.Unsubscribe<UnitPoiseChangedEvent>(HandlePoiseChanged);
    }

    private void HandleBossEncounter(BossEncounterEvent evt)
    {
        _boundBossObject = evt.BossObject;

        if (Model != null)
        {
            Model.BossName = evt.BossName;
            Model.CurrentHp = evt.InitialHp;
            Model.MaxHp = evt.MaxHp;
            Model.CurrentPoise = evt.InitialPoise;
            Model.MaxPoise = evt.MaxPoise;
            Model.IsActive = true;
        }

        if (View != null)
        {
            View.SetBossIdentity(evt.BossName, evt.InitialHp, evt.MaxHp, evt.InitialPoise, evt.MaxPoise);
            View.ShowAsync(0.5f).Forget();
        }
    }

    private void HandleBossDefeated(BossDefeatedEvent evt)
    {
        if (ReferenceEquals(_boundBossObject, evt.BossObject))
        {
            if (View != null)
            {
                View.HideAsync(1.0f).Forget();
            }
            if (Model != null)
            {
                Model.IsActive = false;
            }
            _boundBossObject = null;
        }
    }

    private void HandleHpChanged(UnitHpChangedEvent evt)
    {
        if (View == null || _boundBossObject == null) return;

        if (ReferenceEquals(evt.TargetUnit, _boundBossObject))
        {
            if (Model != null)
            {
                Model.CurrentHp = evt.CurrentHp;
                Model.MaxHp = evt.MaxHp;
            }
            View.UpdateHp(evt.CurrentHp, evt.MaxHp);
        }
    }

    private void HandlePoiseChanged(UnitPoiseChangedEvent evt)
    {
        if (View == null || _boundBossObject == null) return;

        if (ReferenceEquals(evt.TargetUnit, _boundBossObject))
        {
            if (Model != null)
            {
                Model.CurrentPoise = evt.CurrentPoise;
                Model.MaxPoise = evt.MaxPoise;
            }
            View.UpdatePoise(evt.CurrentPoise, evt.MaxPoise);
        }
    }
}