using Util = DebugLogger<EnemyMotionStatusModule>;

public class EnemyMotionStatusModule : UnitMotionStatusModule
{
    public ModifiableStatus ChaseSpeed { get; }
    public ModifiableStatus PatrolSpeed { get; }

    public EnemyMotionStatusModule(EnemyInfo enemyStat) : base(enemyStat)
    {
        if (enemyStat == null)
        {
            Util.LogError("전달된 EnemyStat이 Null입니다.");
            return;
        }

        ChaseSpeed = new ModifiableStatus(enemyStat.ChaseSpeed);
        PatrolSpeed = new ModifiableStatus(enemyStat.PatrolSpeed);
    }

}
