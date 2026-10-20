public class EnemyStateMachine : UnitStateMachine<EnemyController>
{
    public EnemyStateMachine(EnemyController enemy)
    {
        AddState((int)UnitState.Idle, new EnemyIdleState(enemy, this));
        AddState((int)UnitState.Chase, new EnemyChaseState(enemy, this));
        AddState((int)UnitState.Attack, new EnemyAttackState(enemy, this));
        AddState((int)UnitState.GetHit, new EnemyGetHitState(enemy, this));
        AddState((int)UnitState.Stun, new EnemyStunState(enemy, this));
        AddState((int)UnitState.ParryStagger, new EnemyParryStaggerState(enemy, this));
        AddState((int)UnitState.Die, new EnemyDieState(enemy, this));
        AddState((int)UnitState.Return, new EnemyReturnState(enemy, this));
    }
}
