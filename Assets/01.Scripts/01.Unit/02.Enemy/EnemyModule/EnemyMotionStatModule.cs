using Util = DebugLogger<EnemyMotionStatModule>;

public class EnemyMotionStatModule : UnitMotionStatModule
{
    public ModifiableStat ChaseSpeed { get; }
    public ModifiableStat PatrolSpeed { get; }

    public EnemyMotionStatModule(EnemyStat enemyStat) : base(enemyStat)
    {
        if (enemyStat == null)
        {
            Util.LogError("전달된 EnemyStat이 Null입니다.");
            return;
        }

        ChaseSpeed = new ModifiableStat(enemyStat.ChaseSpeed);
        PatrolSpeed = new ModifiableStat(enemyStat.PatrolSpeed);
    }

}
