public class EnemyAttackState : EnemyBaseState
{
    private EnemyAttackData _currentPattern;
    private float _stateTimer = 0f;
    private const float SafetyTimeout = 3.0f; // 비정상 멈춤 방어

    public EnemyAttackState(EnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        _stateTimer = 0f;
        owner.StopMovementAPI();

        _currentPattern = owner.Combat.CurrentSelectedPattern;
        UnityEngine.Transform target = owner.CurrentTarget;

        if (target == null)
        {
            _enemyStateMachine.ChangeState((int)UnitState.Chase);
            return;
        }

        EnemyAttackContext context = owner.CreateAttackContext();
        owner.Combat.StartAttack(in context, target, in _currentPattern);
    }

    public override void Update()
    {
        base.Update();
        _stateTimer += UnityEngine.Time.deltaTime;

        if(_stateTimer >= SafetyTimeout)
        {
            _enemyStateMachine.ChangeState((int)UnitState.Chase);
            return;
        }

        UnityEngine.Transform target = owner.CurrentTarget;
        float normalizedTime = _enemyAnimController.GetCurrentNormalizedTime(0);

        // 정규화 시간 전달
        EnemyAttackContext context = owner.CreateAttackContext();
        owner.Combat.UpdateAttack(in context, target, in _currentPattern, normalizedTime);

        if(normalizedTime >= _currentPattern.ActionEndNormalized && !_enemyAnimController.IsInTransition(0))
        {
            _enemyStateMachine.ChangeState((int)UnitState.Chase);
        }
    }

    public override void Exit()
    {
        EnemyAttackContext context = owner.CreateAttackContext();
        owner.Combat.StopAttack(in context);
        _stateTimer = 0f;
        base.Exit();
    }
}
