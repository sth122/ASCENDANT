using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Util = DebugLogger<EnemyController>;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyAnimController), typeof(EnemyStatController))]
public abstract class EnemyController : UnitController<EnemyController>, IDamageable
{
    [Header("Runtime PlayerStat")]
    [SerializeField] private EnemyStat _runtimeEnemyStat;

    [SerializeField] protected LayerMask _obstacleLayer;

    [Header("타격 판정 컴포넌트")]
    [SerializeField] protected WeaponSweepHitbox _mainHitbox;

    protected Vector3 _spawnPoint;
    protected Transform _currentTarget;
    protected EnemyMovement _enemyMovement;
    protected EnemyAnimController _enemyAnimController;
    protected EnemyStatController _enemyStatController;
    protected EnemyPerceptionModule _perceptionModule;
    protected EnemyCombatModule _combatModule;

    #region Properties
    public EnemyMovement Movement => _enemyMovement;
    public EnemyAnimController EnemyAnimController => _enemyAnimController;
    public EnemyStatController EnemyStatController => _enemyStatController;
    public EnemyPerceptionModule Perception => _perceptionModule;
    public EnemyCombatModule Combat => _combatModule;
    public Transform CurrentTarget => _currentTarget;
    public Vector3 SpawnPoint => _spawnPoint;
    public WeaponSweepHitbox MainHitbox => _mainHitbox;
    #endregion
    protected override void Awake()
    {
        base.Awake();
        _spawnPoint = transform.position;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        _enemyAnimController = GetComponent<EnemyAnimController>();
        _enemyStatController = GetComponent<EnemyStatController>();

        _enemyMovement = new EnemyMovement(_rigidbody, transform, agent, _groundLayer);

        if (_mainHitbox == null)
        {
            _mainHitbox = GetComponentInChildren<WeaponSweepHitbox>();
        }

        // 지각 모듈 초기화
        _perceptionModule = new EnemyPerceptionModule(transform, _targetLayer, _obstacleLayer);
        _perceptionModule.OnTargetSpotted += HandleTargetSpotted;
        _perceptionModule.OnNoiseHeard += HandleNoiseHeard;

        // 전투 모듈 초기화
        _combatModule = new EnemyCombatModule(_unitId, null, new MeleeAttackStrategy());
    }

    /// <summary>
    /// Start 단계에서 DataManager를 통한 SO 데이터 주입 및 상태 머신 구동
    /// </summary>
    protected virtual void Start()
    {
        InitializeFromDataManager();
    }

    protected virtual void InitializeFromDataManager()
    {
        if (DataManager.Instance != null)
        {
            // 1. EnemyStatSO로부터 런타임 스탯 복제 주입
            if (DataManager.Instance._enemyStatSO != null && DataManager.Instance._enemyStatSO.TryGetData(_unitId, out EnemyStat stat))
            {
                _runtimeEnemyStat = (EnemyStat)stat.Clone();
                _enemyStatController.Init(_runtimeEnemyStat);

                _perceptionModule.SightRadius = _runtimeEnemyStat.SightRadius;
                _perceptionModule.SightAngle = _runtimeEnemyStat.SightAngle;
                _perceptionModule.HearingRadius = _runtimeEnemyStat.HearingRadius;
            }
            else
            {
                Util.LogError($"{gameObject.name}: DataManager에서 UnitId ({_unitId})의 EnemyStat을 찾을 수 없습니다.");
            }

            // 2. EnemyAttackDatabaseSO 바인딩
            if (DataManager.Instance._enemyAttackDatabaseSO != null)
            {
                _combatModule.SetAttackDatabase(DataManager.Instance._enemyAttackDatabaseSO);
            }
            else
            {
                Util.LogError($"{gameObject.name}: DataManager의 _enemyAttackDatabaseSO가 Null입니다.");
            }
        }

        Init();
        _perceptionModule.StartSensorLoop();
    }

    protected override void Update()
    {
        base.Update();
        _combatModule.TickCooldown(Time.deltaTime);
    }

    public virtual void SetTarget(Transform target) => _currentTarget = target;

    public virtual void ClearTarget() => _currentTarget = null;

    #region Movement API
    public void StopMovementAPI() => Movement.StopMovement();
    public void MoveToDestinationAPI(Vector3 targetPosition, float speed) => Movement.MoveToDestination(targetPosition, speed);
    public void SetAgentableAPI(bool isEnable) => Movement.Agent.enabled = isEnable;
    public void FaceTargetAPI(Vector3 targetPosition, float rotationSpeed, float deltaTime) 
        => Movement.FaceTarget(targetPosition, rotationSpeed, deltaTime);
    #endregion

    public bool IsExceedingLeashDistance(float maxDistance)
    {
        Vector3 toSpawn = transform.position - _spawnPoint;
        toSpawn.y = 0f; // 수평 투영
        return toSpawn.sqrMagnitude > (maxDistance * maxDistance);
    }

    /// <summary>
    /// 공격 전략 실행 시 필요한 경량 컨텍스트 생성
    /// </summary>
    public EnemyAttackContext CreateAttackContext()
    {
        float baseAtk = _enemyStatController.EnemyStatSource != null
            ? _enemyStatController.EnemyStatSource.AttackPower
            : 20.0f;

        float basePoise = _enemyStatController.EnemyStatSource != null
            ? _enemyStatController.EnemyStatSource.BasePoiseDamage
            : 15.0f;

        return new EnemyAttackContext(gameObject, transform, _enemyMovement,
            _enemyAnimController, _mainHitbox, baseAtk, basePoise
            );
    }

    #region 센서 가상 콜백
    protected virtual void HandleTargetSpotted(Transform target)
    {
        if (_currentTarget == null)
        {
            SetTarget(target);
            _stateMachine.ChangeState((int)UnitState.Chase);
        }
    }

    protected virtual void HandleNoiseHeard(Vector3 noisePosition) { }
    #endregion

    #region IDamageable 피격 수신부
    public virtual void ApplyDamage(in DamageContext context)
    {
        // 이미 사망한 개체
        if (_stateMachine.CurrentState is EnemyDieState) return;

        _enemyStatController.TakeDamage(context.Damage, context.PoiseDamage);

        // Player 자동 타겟 지정
        if (_currentTarget == null && context.Attacker != null)
        {
            SetTarget(context.Attacker.transform);
        }

        if (_enemyStatController.CurrentHp <= 0f)
        {
            _stateMachine.ChangeState((int)UnitState.Die);
            return;
        }

        _stateMachine.ChangeState((int)UnitState.GetHit, true);
    }
    #endregion

    protected virtual void OnDestroy()
    {
        if (_perceptionModule != null)
        {
            _perceptionModule.OnTargetSpotted -= HandleTargetSpotted;
            _perceptionModule.OnNoiseHeard -= HandleNoiseHeard;
            _perceptionModule.Dispose();
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        // 1. 물리 지면 접지 감지 기즈모
        Movement?.DrawGizmos();

        // 2. 시야 및 청각 센서 기즈모
        _perceptionModule?.DrawGizmos(transform);

        // 3. 스폰 위치 및 최대 추적 한계선(Leash Radius) 렌더링
        Vector3 spawn = Application.isPlaying ? _spawnPoint : transform.position;
        float maxLeash = (_enemyStatController != null && _enemyStatController.EnemyStatSource != null)
            ? _enemyStatController.EnemyStatSource.MaxLeashDistance : 25.0f;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawn, 0.6f); // 스폰 앵커 마커

#if UNITY_EDITOR
        Handles.color = new Color(0.1f, 1.0f, 0.3f, 0.3f);
        Handles.DrawWireDisc(spawn, Vector3.up, maxLeash); // 리쉬 한계선 원형 디스크
#endif

        // 4. 현재 타깃 연결선 및 전투 사거리 렌더링
        if (_currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up * 1.0f, _currentTarget.position + Vector3.up * 1.0f);

            // 기본 근접 사거리 가이드 (적색 와이어 디스크)
            float combatRange = (_enemyStatController != null && _enemyStatController.EnemyStatSource != null)
                ? _enemyStatController.EnemyStatSource.AttackRange : 2.0f;

#if UNITY_EDITOR
            Handles.color = new Color(1.0f, 0.1f, 0.1f, 0.5f);
            Handles.DrawWireDisc(transform.position, Vector3.up, combatRange);
#endif
        }
    }
}
