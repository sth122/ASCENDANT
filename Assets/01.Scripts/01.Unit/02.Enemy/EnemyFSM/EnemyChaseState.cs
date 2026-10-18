public class EnemyChaseState : EnemyBaseState
{
    public EnemyChaseState(EnemyController owner, EnemyStateMachine stateMachine) : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();
        _enemyAnimController.PlayState(AnimStateType.Run);
    }


    public override void Update()
    {
        base.Update();

        UnityEngine.Transform target = owner.CurrentTarget;
        if(target != null)
        {
            _enemyStateMachine.ChangeState((int)UnitState.Idle);
            return;
        }
        
        float maxLeash = _enemyStatController.EnemyStatSource != null
            ? _enemyStatController.EnemyStatSource.MaxLeashDistance : 25.0f;

        if (owner.IsExceedingLeashDistance(maxLeash))
        {
            _enemyStateMachine.ChangeState((int)UnitState.Return);
            return;
        }

        if (owner.Combat.TrySelectAttackPattern(target, out EnemyAttackData selectedPattern))
        {
            _enemyStateMachine.ChangeState((int)UnitState.Attack);
            return;
        }

        float speed = _enemyStatController.EnemyMotionStats != null
            ? _enemyStatController.EnemyMotionStats.ChaseSpeed.Value : 4.5f;

        owner.MoveToDestinationAPI(target.position, speed);
    }
}
