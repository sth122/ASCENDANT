using UnityEngine;

public class EnemyReturnState : EnemyBaseState
{
    private const float ReturnSpeed = 3.5f;
    private const float PathUpdateInterval = 0.5f;
    private float _pathTimer = 0f;

    public EnemyReturnState(EnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        owner.ClearTarget();
        owner.SetStoppingDistanceAPI(UnitState.Patrol);

        owner.SetDestinationAPI(owner.SpawnPoint, ReturnSpeed);
        _enemyAnimController.PlayState(AnimStateType.Walk);
    }

    public override void Update()
    {
        base.Update();
        owner.TickLocomotionRotationAPI(Time.deltaTime, 4.0f);

        Vector3 toSpawn = owner.transform.position - owner.SpawnPoint;
        toSpawn.y = 0f;
        if (toSpawn.sqrMagnitude <= (1.2f * 1.2f))
        {
            _enemyStateMachine.ChangeState((int)UnitState.Idle);
        }
    }
}
