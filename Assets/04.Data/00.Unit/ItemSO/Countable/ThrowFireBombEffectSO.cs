/// <summary>
/// 지정된 풀링 투사체를 전방으로 투척하는 아이템 효과
/// </summary>
[UnityEngine.CreateAssetMenu(fileName = "ThrowFireBombEffect", menuName = "Inventory/ItemEffect/ThrowFireBomb")]
public class ThrowFireBombEffectSO : ItemEffectSO
{
    [UnityEngine.SerializeField] private float _bombDamage = 180f;
    [UnityEngine.SerializeField] private float _bombPoiseDamage = 45f;
    [UnityEngine.SerializeField] private float _throwSpeed = 14f;
    [UnityEngine.SerializeField] private UnityEngine.Vector3 _spawnOffset = new UnityEngine.Vector3(0f, 1.4f, 0.7f);

    public override bool Execute(UnityEngine.GameObject user)
    {
        if (user == null || ObjectPoolManager.Instance == null) return false;

        UnityEngine.Transform userTr = user.transform;
        UnityEngine.Vector3 spawnPos = userTr.position + userTr.up * _spawnOffset.y + userTr.forward * _spawnOffset.z;

        // 투사체 발사 속도 벡터 연산 (전방 + 상향 각도 포물선)
        UnityEngine.Vector3 launchVelocity = (userTr.forward + userTr.up * 0.35f).normalized * _throwSpeed;

        FireBombProjectile projectile = ObjectPoolManager.Instance.Spawn<FireBombProjectile>(
            PoolType.FireBomb, spawnPos, UnityEngine.Quaternion.LookRotation(launchVelocity) // 임시 투사체 PoolType 재사용 또는 신규 매핑
        );

        if (projectile != null)
        {
            projectile.Launch(user, _bombDamage, _bombPoiseDamage, launchVelocity);
            return true;
        }

        return false;
    }
}