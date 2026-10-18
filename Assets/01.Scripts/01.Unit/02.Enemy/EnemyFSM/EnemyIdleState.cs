public class EnemyIdleState : EnemyBaseState
{
    public EnemyIdleState(EnemyController owner, EnemyStateMachine stateMachine) : base(owner, stateMachine)
    {}

    public override void Enter()
    {
        base.Enter();
        owner.StopMovementAPI();
        _enemyAnimController.PlayState(AnimStateType.Idle);
    }
}
