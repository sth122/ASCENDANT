public class EnemyChaseState : EnemyBaseState
{
    private const float PathUpdateInterval = 0.2f;
    private float _pathUpdateTimer = 0f;
    private bool _isInCombatRange = false;

    public EnemyChaseState(EnemyController owner, EnemyStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        _pathUpdateTimer = 0f; 

        owner.SetStoppingDistanceAPI(UnitState.Chase);

        float engageRange = GetEngageDistance();

        // 진입 시점에 이미 공격 사거리 이내라면 Run을 켜지 않고 BattleIdle로 시작
        if (owner.CurrentTarget != null)
        {
            UnityEngine.Vector3 toTarget = owner.CurrentTarget.position - owner.transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= engageRange)
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
        if(target == null)
        {
            _enemyStateMachine.ChangeState((int)UnitState.Idle);
            return;
        }

        // 2. 타겟이 사망했으면 타겟을 해제하고 복귀
        UnitStatController targetStat = target.GetComponentInParent<UnitStatController>();
        if (targetStat != null && targetStat.IsDead)
        {
            owner.ClearTarget();
            _enemyStateMachine.ChangeState((int)UnitState.Return);
            return;
        }
        
        // 3. 최대 활동 반경 초과 검사
        float maxLeash = _enemyStatController.EnemyStatSource != null
            ? _enemyStatController.EnemyStatSource.MaxLeashDistance : 25.0f;

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
        float engageRange = GetEngageDistance();

        // 5. 사거리 내 진입 시: 정지 후 BattleIdle 모션으로 대치
        if (distance <= engageRange)
        {
            if (!_isInCombatRange)
            {
                _isInCombatRange = true;
                owner.StopMovementAPI();
                _enemyAnimController.PlayState(AnimStateType.BattleIdle);
            }

            // 대치 중에는 제자리에서 플레이어를 지속 주시
            owner.FaceTargetAPI(target.position, 5.0f, UnityEngine.Time.deltaTime);

            // 쿨타임이 완료되어 공격 패턴 실행이 가능한 경우 BattleIdle에서 Attack으로 자연스럽게 진입
            EnemyAttackContext context = owner.CreateAttackContext();
            if (owner.Combat.TrySelectAttackPattern(in context, target, out EnemyAttackData selectedPattern))
            {
                _enemyStateMachine.ChangeState((int)UnitState.Attack);
                return;
            }
        }
        else
        {
            // 6. 플레이어가 사거리 밖으로 벗어난 경우: Run 모션으로 재추격
            if (_isInCombatRange)
            {
                _isInCombatRange = false;
                _enemyAnimController.PlayState(AnimStateType.Run);
            }

            // 0.2초 스로틀링 경로 갱신
            _pathUpdateTimer += UnityEngine.Time.deltaTime;
            if (_pathUpdateTimer >= PathUpdateInterval)
            {
                _pathUpdateTimer = 0f;
                float speed = _enemyStatController.EnemyMotionStats != null
                    ? _enemyStatController.EnemyMotionStats.ChaseSpeed.Value : 4.5f;

                owner.SetDestinationAPI(target.position, speed);
            }

            owner.TickLocomotionRotationAPI(UnityEngine.Time.deltaTime, 4.5f);
        }
    }

    /// <summary>
    /// 공격 데이터베이스 상의 최대 공격 사거리에 마진을 더한 실제 대치 진입 사거리 산출
    /// </summary>
    private float GetEngageDistance()
    {
        float maxPatternRange = owner.Combat != null ? owner.Combat.GetMaxAttackRange() : 2.0f;
        // 최대 공격 사거리(2.0m)보다 살짝 넓은 2.3m를 대치 기준점으로 설정
        return UnityEngine.Mathf.Max(2.3f, maxPatternRange + 0.3f);
    }
    public override void Exit()
    {
        base.Exit();
        owner.StopMovementAPI();
        _isInCombatRange = false;
    }
}
