using UnityEngine;

public class EnemyIdleState : EnemyBaseState
{
    private const float IdleWaitDuration = 2.5f;
    private float _timer = 0f;

    public EnemyIdleState(EnemyController owner, EnemyStateMachine stateMachine) : base(owner, stateMachine)
    {}

    public override void Enter()
    {
        base.Enter();
        _timer = 0f;
        owner.StopMovementAPI();
        _enemyAnimController.PlayState(AnimStateType.Idle);
    }

    public override void Update()
    {
        base.Update();

        // 타깃이 식별되면 즉시 추적 전이
        if (owner.CurrentTarget != null)
        {
            _enemyStateMachine.ChangeState((int)UnitState.Chase);
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= IdleWaitDuration)
        {
            // 크리처 유닛인 경우 고유 포복 순찰(Crawl)로 복귀
            if (owner is CreatureEnemyController)
            {
                _enemyStateMachine.ChangeState((int)UnitState.Patrol);
            }
            else
            {
                _enemyStateMachine.ChangeState((int)UnitState.Return);
            }
        }
    }
}
