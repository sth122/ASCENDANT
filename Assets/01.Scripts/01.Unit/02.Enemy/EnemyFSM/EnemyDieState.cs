using UnityEngine;

public class EnemyDieState : EnemyBaseState
{
    public EnemyDieState(EnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        owner.StopMovementAPI();
        _enemyAnimController.PlayState(AnimStateType.Death1);

        owner.Perception.StopSensorLoop();

        if (owner.Collider != null)
        {
            owner.Collider.enabled = false;
        }

        if (owner.Movement.Agent != null)
        {
            owner.SetAgentableAPI(false);
        }

        owner.enabled = false;
    }
}
