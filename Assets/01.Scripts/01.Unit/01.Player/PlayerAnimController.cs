using UnityEngine;

public class PlayerAnimController : UnitAnimController, IAnimationCombatEvent
{
    [SerializeField] private WeaponSweepHitbox _weaponHitbox;
    public WeaponSweepHitbox WeaponHitbox => _weaponHitbox;
    public event System.Action OnComboWindowOpenEvent;
    public event System.Action OnComboWindowCloseEvent;
    public event System.Action OnHitboxOpenEvent;
    public event System.Action OnHitboxCloseEvent;
    public event System.Action OnActionCompletedEvent;
    protected override void Awake()
    {
        base.Awake();
        if (_weaponHitbox == null)
            _weaponHitbox = GetComponentInChildren<WeaponSweepHitbox>();
    }

    #region MyRegion
    public void Anim_OnComboWindowOpen() => OnComboWindowOpenEvent?.Invoke();
    public void Anim_OnComboWindowClose() => OnComboWindowCloseEvent?.Invoke();
    public void Anim_OnHitboxOpen() => OnHitboxOpenEvent?.Invoke();
    public void Anim_OnHitboxClose() => OnHitboxCloseEvent?.Invoke();
    public void Anim_OnActionCompleted() => OnActionCompletedEvent?.Invoke();
    #endregion
}
