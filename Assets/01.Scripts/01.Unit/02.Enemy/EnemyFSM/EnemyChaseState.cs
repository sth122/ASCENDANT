public class EnemyChaseState : EnemyBaseState
{
    private const float PathUpdateInterval = 0.2f;
    private float _pathUpdateTimer = 0f;
    private bool _isInCombatRange = false;

    // 대치 진입 및 이탈 사거리
    private const float EngageDistance = 1.8f;      // 대치 진입 거리 (공격 사거리 2.0m보다 앞쪽)
    private const float DisengageDistance = 2.8f;   // 대치 이탈 거리

    public EnemyChaseState(EnemyController owner, EnemyStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        _pathUpdateTimer = 0f;

        owner.SetStoppingDistanceAPI(UnitState.Chase);

        // 진입 시점에 이미 공격 사거리 이내라면 Run을 켜지 않고 BattleIdle로 시작
        if (owner.CurrentTarget != null)
        {
            UnityEngine.Vector3 toTarget = owner.CurrentTarget.position - owner.transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= EngageDistance)
            {
                _isInCombatRange = true;
                owner.StopMovementAPI();
                _enemyAnimController.PlayState(AnimStateType.BattleIdle);
                return;
            }
        }

        // 사거리 밖인 경우에만 Run 모션으로 추격 시작
        _isInCombatRange = false;
        _enemyAnimController.PlayState(AnimStateType.Run);
    }


    public override void Update()
    {
        base.Update();

        UnityEngine.Transform target = owner.CurrentTarget;
        // 1. 타겟이 없으면 대기 상태로 전이
        if (target == null)
        {
            _enemyStateMachine.ChangeState((int)UnitState.Idle);
            return;
        }

        // 2. 타겟이 사망했으면 타겟을 해제하고 복귀
        UnitStatusController targetStat = target.GetComponentInParent<UnitStatusController>();
        if (targetStat != null && targetStat.IsDead)
        {
            owner.ClearTarget();
            _enemyStateMachine.ChangeState((int)UnitState.Return);
            return;
        }

        // 3. 최대 활동 반경 초과 검사
        float maxLeash = _enemyStatusController.EnemyStatusSource != null
            ? _enemyStatusController.EnemyStatusSource.MaxLeashDistance : 25.0f;

        if (owner.IsExceedingLeashDistance(maxLeash))
        {
            _enemyStateMachine.ChangeState((int)UnitState.Return);
            return;
        }

        // 4. 플레이어와의 수평 거리 산출
        UnityEngine.Vector3 toTarget = target.position - owner.transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        // 기본 공격 사거리 (기준: 2.0m)

        // 5. 대치 영역 상태 분기
        if (!_isInCombatRange && distance <= EngageDistance)
        {
            EnterCombatStance();
        }
        else if (_isInCombatRange && distance > DisengageDistance)
        {
            _isInCombatRange = false;
            _enemyAnimController.PlayState(AnimStateType.Run);
        }

        // 6. 대치 분기
        if (_isInCombatRange)
        {
            owner.FaceTargetAPI(target.position, 5.0f, UnityEngine.Time.deltaTime);

            EnemyAttackContext context = owner.CreateAttackContext();
            if (owner.Combat.TrySelectAttackPattern(in context, target, out _))
            {
                _enemyStateMachine.ChangeState((int)UnitState.Attack);
                return;
            }
        }
        else // 추격 로직
        {
            _pathUpdateTimer += UnityEngine.Time.deltaTime;
            if (_pathUpdateTimer >= PathUpdateInterval)
            {
                _pathUpdateTimer = 0f;
                float speed = _enemyStatusController.EnemyMotionStatus != null
                    ? _enemyStatusController.EnemyMotionStatus.ChaseSpeed.Value : 4.5f;

                owner.SetDestinationAPI(target.position, speed);
            }

            owner.TickLocomotionRotationAPI(UnityEngine.Time.deltaTime, 4.5f);
        }
    }

    public void EnterCombatStance()
    {
        _isInCombatRange = true;
        owner.StopMovementAPI();


        _enemyAnimController.PlayState(AnimStateType.BattleIdle);
    }
    public override void Exit()
    {
        base.Exit();
        owner.StopMovementAPI();
        _isInCombatRange = false;
    }
}
