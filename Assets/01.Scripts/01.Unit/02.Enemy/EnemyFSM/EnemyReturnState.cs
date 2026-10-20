using UnityEngine;

public class EnemyReturnState : EnemyBaseState
{
    private const float ReturnSpeed = 3.5f;

    public EnemyReturnState(EnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        owner.ClearTarget();
        owner.SetStoppingDistanceAPI(UnitState.Patrol);
        _enemyAnimController.PlayState(AnimStateType.Walk);
    }

    public override void Update()
    {
        base.Update();
        owner.SetDestinationAPI(owner.SpawnPoint, ReturnSpeed);

        Vector3 toSpawn = owner.transform.position - owner.SpawnPoint;
        toSpawn.y = 0f;
        if (toSpawn.sqrMagnitude <= (1.0f * 1.0f))
        {
            _enemyStateMachine.ChangeState((int)UnitState.Idle);
        }
    }
}
