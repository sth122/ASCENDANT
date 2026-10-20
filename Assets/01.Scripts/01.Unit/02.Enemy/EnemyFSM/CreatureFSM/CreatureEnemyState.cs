public class CreatureEnemyStateMachine : EnemyStateMachine
{
    public CreatureEnemyStateMachine(CreatureEnemyController creature)
        : base(creature)
    {
        AddState((int)UnitState.Patrol, new CreatureCrawlState(creature, this));
        AddState((int)UnitState.Alert, new CreatureAlertState(creature, this));
        AddState((int)UnitState.Roar, new CreatureRoarState(creature, this));
    }
}