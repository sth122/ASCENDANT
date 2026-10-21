/// <summary>
/// Enemy 공격 상태를 전담하는 Enemy 상태 FSM 클래스
/// </summary>
public class EnemyAttackState : EnemyBaseState
{
    private EnemyAttackData _currentPattern;
    private int _targetAnimHash;
    private float _stateTimer = 0f;
    private const float SafetyTimeout = 3.0f; // 비정상 멈춤 방어
    private const float TransitionLockoutTime = 0.15f; // CrossFade 진입 보장 최소 시간

    public EnemyAttackState(EnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        _stateTimer = 0f;
        owner.StopMovementAPI();

        _currentPattern = owner.Combat.CurrentSelectedPattern;

        if (owner.CurrentTarget == null)
        {
            _enemyStateMachine.ChangeState((int)UnitState.Chase);
            return;
        }

        // 재생할 공격 클립의 해시값 캐싱
        _targetAnimHash = _enemyAnimController.GetStateHash(_currentPattern.AttackAnimStateType);

        EnemyAttackContext context = owner.CreateAttackContext();
        owner.Combat.StartAttack(in context, owner.CurrentTarget, in _currentPattern);
    }

    public override void Update()
    {
        base.Update();
        _stateTimer += UnityEngine.Time.deltaTime;

        // 1. 타임아웃 비상 탈출
        if(_stateTimer >= SafetyTimeout)
        {
            _enemyStateMachine.ChangeState((int)UnitState.Chase);
            return;
        }

        // 2. CrossFade 시작 직후 최소 락아웃 동안은 이전 상태(BattleIdle)의 시간을 읽지 않도록 방어
        if (_stateTimer < TransitionLockoutTime)
        {
            // 선딜레이 회전 추적만 안전하게 유지
            if (owner.CurrentTarget != null)
            {
                owner.FaceTargetAPI(owner.CurrentTarget.position, 5.0f, UnityEngine.Time.deltaTime);
            }
            return;
        }

        // 3. 실제 공격 클립의 normalizedTime 추출
        float normalizedTime = _enemyAnimController.GetCurrentNormalizedTimeOfState(_targetAnimHash, 0);

        // 4. 공격 클립을 재생하지 않았으면 판정 유예
        if (normalizedTime <= 0f) return;

        // 정규화 시간 전달
        EnemyAttackContext context = owner.CreateAttackContext();
        owner.Combat.UpdateAttack(in context, owner.CurrentTarget, in _currentPattern, normalizedTime);

        if(normalizedTime >= _currentPattern.ActionEndNormalized && !_enemyAnimController.IsInTransition(0))
        {
            _enemyStateMachine.ChangeState((int)UnitState.Chase);
        }
    }

    public override void Exit()
    {
        EnemyAttackContext context = owner.CreateAttackContext();

        float globalCooldown = _enemyStatusController.EnemyStatusSource != null
            ? _enemyStatusController.EnemyStatusSource.AttackCooldown : 4f;

        owner.Combat.StopAttack(in context, globalCooldown);
        _stateTimer = 0f;
        base.Exit();
    }
}
