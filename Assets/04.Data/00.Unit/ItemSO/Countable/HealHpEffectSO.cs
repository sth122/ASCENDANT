/// <summary>
/// 플레이어의 체력을 즉시 회복시키는 아이템 효과
/// </summary>
[UnityEngine.CreateAssetMenu(fileName = "HealHpEffect", menuName = "Inventory/ItemEffect/HealHp")]
public class HealHpEffectSO : ItemEffectSO
{
    [UnityEngine.SerializeField] private float _healAmount = 250f;

    public override bool Execute(UnityEngine.GameObject user)
    {
        if (user == null) return false;

        UnitStatusController status = user.GetComponent<UnitStatusController>();
        if (status != null && !status.IsDead)
        {
            status.HealHp(_healAmount);
            return true;
        }

        return false;
    }
}