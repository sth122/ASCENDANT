public class EnemyChaseState : EnemyBaseState
{
    private const float PathUpdateInterval = 0.2f;
    private float _pathUpdateTimer = 0f;

    public EnemyChaseState(EnemyController owner, EnemyStateMachine stateMachine) : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();
        _enemyAnimController.PlayState(AnimStateType.Run);
        _pathUpdateTimer = 0f;
    }


    public override void Update()
    {
        base.Update();

        UnityEngine.Transform target = owner.CurrentTarget;
        // 1. 타겟이 없거나 사망했으면 대기 상태로 전이
        if(target != null)
        {
            _enemyStateMachine.ChangeState((int)UnitState.Idle);
            return;
        }

        UnitStatController targetStat = target.GetComponentInParent<UnitStatController>();
        if (targetStat != null && targetStat.IsDead)
        {
            owner.ClearTarget();
            _enemyStateMachine.ChangeState((int)UnitState.Return);
            return;
        }
        
        // 최대 활동 반경 검사
        float maxLeash = _enemyStatController.EnemyStatSource != null
            ? _enemyStatController.EnemyStatSource.MaxLeashDistance : 25.0f;

        if (owner.IsExceedingLeashDistance(maxLeash))
        {
            _enemyStateMachine.ChangeState((int)UnitState.Return);
            return;
        }

        // 3. 공격 사거리 및 패턴 검사
        EnemyAttackContext context = owner.CreateAttackContext();
        if (owner.Combat.TrySelectAttackPattern(in context, target, out EnemyAttackData selectedPattern))
        {
            _enemyStateMachine.ChangeState((int)UnitState.Attack);
            return;
        }

        // 4. 경로 갱신 적용
        _pathUpdateTimer += UnityEngine.Time.deltaTime;
        if (_pathUpdateTimer >= PathUpdateInterval)
        {
            _pathUpdateTimer = 0f;
            float speed = _enemyStatController.EnemyMotionStats != null
                ? _enemyStatController.EnemyMotionStats.ChaseSpeed.Value : 4.5f;

            owner.MoveToDestinationAPI(target.position, speed);
        }
    }

    public override void Exit()
    {
        base.Exit();
        owner.StopMovementAPI();
    }
}
