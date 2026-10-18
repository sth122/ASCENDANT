/// <summary>
/// 공격 전략 실행 시 필요한 최소 참조 및 본체 기본 공격 스탯을 전달하는 경량 컨텍스트 구조체
/// </summary>
public readonly struct EnemyAttackContext
{
    public readonly UnityEngine.GameObject AttackerObject;
    public readonly UnityEngine.Transform OwnerTransform;
    public readonly EnemyMovement Movement;
    public readonly EnemyAnimController AnimController;
    public readonly WeaponSweepHitbox MainHitbox;
    public readonly float BaseAttackPower;
    public readonly float BasePoiseDamage;

    public EnemyAttackContext(UnityEngine.GameObject attackerObject,  UnityEngine.Transform ownerTransform,
        EnemyMovement movement, EnemyAnimController animController, WeaponSweepHitbox mainHitbox,
        float baseAttackPower, float basePoiseDamage)
    {
        AttackerObject = attackerObject;
        OwnerTransform = ownerTransform;
        Movement = movement;
        AnimController = animController;
        MainHitbox = mainHitbox;
        BaseAttackPower = baseAttackPower;
        BasePoiseDamage = basePoiseDamage;
    }
}
