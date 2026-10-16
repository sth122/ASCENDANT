public class PlayerAnimController : UnitAnimController, IAnimationCombatEvent
{
    [UnityEngine.SerializeField] private WeaponSweepHitbox _weaponHitbox;
    public WeaponSweepHitbox WeaponHitbox => _weaponHitbox;
    public event System.Action OnComboWindowOpenEvent;
    public event System.Action OnComboWindowCloseEvent;
    protected override void Awake()
    {
        base.Awake();
        if (_weaponHitbox == null)
            _weaponHitbox = GetComponentInChildren<WeaponSweepHitbox>();
    }

    #region IAnimationCombatEvent 구현부
    public void Anim_OnComboWindowOpen() => OnComboWindowOpenEvent?.Invoke();
    public void Anim_OnComboWindowClose() => OnComboWindowCloseEvent?.Invoke();
    #endregion
}
