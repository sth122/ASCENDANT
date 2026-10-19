public class PlayerAnimController : UnitAnimController
{
    [UnityEngine.SerializeField] private WeaponSweepHitbox _weaponHitbox;
    public WeaponSweepHitbox WeaponHitbox => _weaponHitbox;

    protected override void Awake()
    {
        base.Awake();
        if (_weaponHitbox == null)
            _weaponHitbox = GetComponentInChildren<WeaponSweepHitbox>();
    }
}
