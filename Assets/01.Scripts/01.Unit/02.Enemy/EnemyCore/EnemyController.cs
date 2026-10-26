using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Util = DebugLogger<EnemyController>;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyAnimController), typeof(EnemyStatusController))]
public abstract class EnemyController : UnitController<EnemyController>, IDamageable, IPoolable
{
    #region 인스펙터 직렬화 필드
    [Header("Layer Masks")]
    [SerializeField] protected LayerMask _obstacleLayer;

    [Header("타격 판정 컴포넌트")]
    [SerializeField] protected WeaponSweepHitbox _mainHitbox;

    [Header("Locomotion Stopping Distances")]
    [SerializeField] private float _chaseStoppingDistance = 1.6f;
    [SerializeField] private float _patrolStoppingDistance = 0.5f;

    [Header("Respawn & Boss Settings (소울라이크 리젠 설정)")]
    [Tooltip("체크 해제 시 화톳불에서 쉬어도 부활하지 않는 보스/특수 개체로 취급")]
    [SerializeField] protected bool _isRespawnable = true;

    [Tooltip("보스/특수 몬스터의 영구 사망 여부를 판별하기 위한 고유 키 (일반 잡몹은 비워둠)")]
    [SerializeField] protected string _permanentSpawnId = string.Empty;
    #endregion

    #region 초기 상태 보존 변수)
    protected Vector3 _originPosition;
    protected Quaternion _originRotation;
    protected float _originMaxHp;
    protected bool _isOriginCaptured = false;
    #endregion

    #region 내부 런타임 필드
    protected Vector3 _spawnPoint;
    protected Transform _currentTarget;
    protected EnemyTier _tier = EnemyTier.Normal;

    protected EnemyMovement _enemyMovement;
    protected EnemyAnimController _enemyAnimController;
    protected EnemyPerceptionModule _perceptionModule;
    protected EnemyCombatModule _combatModule;
    #endregion

    #region Properties
    public EnemyMovement Movement => _enemyMovement;
    public EnemyAnimController EnemyAnimController => _enemyAnimController;
    public EnemyStatusController StatusController => _unitStatusController as EnemyStatusController;
    public EnemyPerceptionModule Perception => _perceptionModule;
    public EnemyCombatModule Combat => _combatModule;
    public Transform CurrentTarget => _currentTarget;
    public Vector3 SpawnPoint => _spawnPoint;
    public WeaponSweepHitbox MainHitbox => _mainHitbox;
    public virtual EnemyTier Tier => _tier;
    public bool CanBeRiposted { get; set; } = false;
    public bool IsRespawnable => _isRespawnable;
    public string PermanentSpawnId => _permanentSpawnId;
    #endregion

    private EnemyFloatingHUDPresenter _hudPresenter;
    protected override void Awake()
    {
        base.Awake();
        SetupComponents();
    }

    private void SetupComponents()
    {
        CaptureOriginTransform();

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        _enemyAnimController = GetComponent<EnemyAnimController>();
        _hudPresenter = GetComponent<EnemyFloatingHUDPresenter>();

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
    /// 최초 스폰 좌표와 회전값을 영구 기억
    /// </summary>
    public void CaptureOriginTransform()
    {
        if (!_isOriginCaptured)
        {
            _originPosition = transform.position;
            _originRotation = transform.rotation;
            _spawnPoint = _originPosition;
            _isOriginCaptured = true;
        }
    }

    #region 데이터 주입 및 바인딩
    /// <summary>
    /// DataManager 또는 외부 스포너로부터 전달받은 최신 기획 데이터를 주입하는 메서드
    /// </summary>
    /// <param name="overrideStat">스포너 등에서 직접 주입할 스탯 (null일 경우 DataManager에서 자동 조회)</param>
    public void BindStatusData(EnemyStatus overrideStat = null)
    {
        // 1. EnemyStatus 주입 및 동기화
        if (overrideStat != null)
        {
            ApplyStatus(overrideStat);
        }
        else if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<EnemyStatusSO>(out EnemyStatusSO statusSO))
        {
            if (statusSO.TryGetData(_unitId, out EnemyStatus baseStatus))
            {
                EnemyStatus runtimeStatus = (EnemyStatus)baseStatus.Clone();
                ApplyStatus(runtimeStatus);
            }
            else
            {
                Util.LogError($"{gameObject.name}: UnitId ({_unitId})에 해당하는 스탯이 EnemyStatusSO에 없습니다.");
            }
        }
        else
        {
            Util.LogWarning($"{gameObject.name}: DataManager에서 EnemyStatusSO를 찾을 수 없습니다.");
        }

        // 2. EnemyAttackDatabaseSO 주입
        if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<EnemyAttackDatabaseSO>(out EnemyAttackDatabaseSO attackDbSO))
        {
            _combatModule.SetAttackDatabase(attackDbSO);
        }
        else
        {
            Util.LogWarning($"{gameObject.name}: DataManager에서 EnemyAttackDatabaseSO를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 확정된 EnemyStatus 데이터를 바탕으로 런타임 수치 및 센서 사양을 동기화
    /// </summary>
    private void ApplyStatus(EnemyStatus status)
    {
        _tier = status.Tier;
        StatusController.Init(status);

        _originMaxHp = StatusController.MaxHp;

        if (_perceptionModule != null)
        {
            _perceptionModule.SightRadius = status.SightRadius;
            _perceptionModule.SightAngle = status.SightAngle;
            _perceptionModule.HearingRadius = status.HearingRadius;
        }
    }
    #endregion

    #region IPoolable 구현부
    public virtual void OnSpawn() { }
    public virtual void OnDespawn()
    {
        _hudPresenter?.ReleaseView();
        _perceptionModule?.StopSensorLoop();
        StopMovementAPI();
        _currentTarget = null;
    }
    #endregion

    #region 월드 스폰 및 AI 기동
    /// <summary>
    /// 씬에 정적으로 배치된 적의 경우 Start 단계에서 자가 초기화 실행
    /// </summary>
    protected virtual async void Start()
    {
        if (!gameObject.activeInHierarchy) return;

        // DataManager 준비 대기 후 초기화
        if (DataManager.Instance != null && !DataManager.Instance.IsDataLoaded)
        {
            await UniTask.WaitUntil(CheckDataLoaded, cancellationToken: this.destroyCancellationToken);
        }

        if (ObjectPoolManager.Instance != null)
        {
            await ObjectPoolManager.Instance.EnsurePoolReadyAsync(this.destroyCancellationToken);
        }

        // EnemyManager 서비스에 자기 자신 자동 등록
        RegisterToEnemyManager();

        // 보스/네임드인 경우 이미 세이브/플래그 상에서 처치되었는지 검증
        if (!_isRespawnable && !string.IsNullOrEmpty(_permanentSpawnId))
        {
            if (ServiceLocator.TryGet<IEnemyManager>(out IEnemyManager enemyManager))
            {
                if (enemyManager.IsPermanentEnemyDefeated(_permanentSpawnId))
                {
                    gameObject.SetActive(false);
                    return;
                }
            }
        }

        SpawnInit(_originPosition);
    }

    private bool CheckDataLoaded() => DataManager.Instance != null && DataManager.Instance.IsDataLoaded;

    private void RegisterToEnemyManager()
    {
        if (ServiceLocator.TryGet<IEnemyManager>(out IEnemyManager enemyManager))
        {
            enemyManager.RegisterEnemy(this);
        }
    }

    /// <summary>
    /// 스포너나 오브젝트 풀에서 소환될 때 호출되는 최종 활성화 진입점
    /// 위치 보정 -> 데이터 바인딩 -> FSM 재가동 -> 센서 활성화 순서로 수행됩니다.
    /// </summary>
    /// <param name="spawnPosition">스폰될 월드 좌표</param>
    /// <param name="overrideStat">특수 튜닝된 런타임 스탯 (선택 사항)</param>
    public virtual void SpawnInit(Vector3 spawnPosition, EnemyStatus overrideStat = null)
    {
        _spawnPoint = spawnPosition;
        transform.position = spawnPosition;

        // 1. 물리 및 NavMesh 에이전트 안전 동기화
        ResetPhysicsAndNavMesh(spawnPosition);

        // 2. 최신 기획 데이터 바인딩
        BindStatusData(overrideStat);

        // 3. FSM 상태 머신 초기화]
        Init();

        // 4. 지각 센서 비동기 감지 루프 시작
        _perceptionModule?.StartSensorLoop();

        this.enabled = true;

        // Presenter에게 스폰 및 뷰 배정 요청
        if (_hudPresenter != null)
        {
            _hudPresenter.AllocateAndBindView();
        }

        Util.Log($"{gameObject.name} (ID: {_unitId}) 스폰 및 기동 완료");
    }

    /// <summary>
    /// 스폰 좌표로 NavMeshAgent와 물리 트랜스폼을 오차 없이 안전하게 정렬
    /// </summary>
    private void ResetPhysicsAndNavMesh(Vector3 targetPosition)
    {
        if (Movement.Agent != null)
        {
            Movement.Agent.enabled = false;
            transform.position = targetPosition;
            Movement.Agent.enabled = true;

            if (Movement.Agent.isOnNavMesh)
            {
                Movement.Agent.Warp(targetPosition);
            }
            else
            {
                if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    Movement.Agent.Warp(hit.position);
                }
            }
        }
    }
    #endregion

    #region 화톳불 초기화 프로세스
    /// <summary>
    /// 화톳불 휴식 시 EnemyManager의 신호를 받아 상태에 따라 3가지 분기로 복원하는 메서드
    /// </summary>
    public virtual void ResetToOriginState()
    {
        // [분기 ③] 이미 죽었고 리젠되면 안 되는 에너미 (보스/네임드)
        if (!_isRespawnable && !string.IsNullOrEmpty(_permanentSpawnId))
        {
            if (ServiceLocator.TryGet<IEnemyManager>(out IEnemyManager enemyManager))
            {
                if (enemyManager.IsPermanentEnemyDefeated(_permanentSpawnId) || StatusController.IsDead)
                {
                    _hudPresenter?.ReleaseView();
                    // 처치 목록에 등록되어 있거나 이미 죽어있다면 리셋을 무시하고 비활성화 유지
                    gameObject.SetActive(false);
                    return;
                }
            }
        }

        // 이미 죽어서 맵에서 비활성화된 에너미 복원
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        // 1. 컴포넌트 및 조작 플래그 원상복구
        this.enabled = true;
        CanBeRiposted = false;
        ClearTarget();

        if (_collider != null)
        {
            _collider.enabled = true;
            _collider.isTrigger = false;
        }

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
        }

        // 2. 가동 중이던 공격 취소 및 히트박스 회수
        EnemyAttackContext attackContext = CreateAttackContext();
        Combat.StopAttack(in attackContext, 0f);

        // 3. 최초 저장해 둔 originPosition과 originRotation으로 좌표 및 회전 즉시 스냅
        if (Movement.Agent != null)
        {
            Movement.Agent.enabled = false;
            transform.position = _originPosition;
            transform.rotation = _originRotation;
            Movement.Agent.enabled = true;

            if (Movement.Agent.isOnNavMesh)
            {
                Movement.Agent.Warp(_originPosition);
            }
            else
            {
                if (NavMesh.SamplePosition(_originPosition, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                {
                    Movement.Agent.Warp(hit.position);
                }
            }
        }
        else
        {
            transform.position = _originPosition;
            transform.rotation = _originRotation;
        }

        // 4. 체력과 강인도를 maxHealth로 가득 채움
        StatusController.SetHpDirectly(_originMaxHp > 0f ? _originMaxHp : StatusController.MaxHp);
        StatusController.RestorePoiseToMax();
            
        // 5. AI 시스템(FSM)을 초기 순찰 또는 대기 상태로 재설정
        ResetFSMToDefaultState();

        // 6. 감각 센서 루프 재개
        _perceptionModule?.StartSensorLoop();

        // 화톳불 복귀 시 뷰 재배정
        if (_hudPresenter != null)
        {
            _hudPresenter.AllocateAndBindView();
        }

        Util.Log($"[{gameObject.name}] 화톳불 리셋 완료 -> Origin 위치로 복귀 및 HP 회복 완료");
    }

    /// <summary>
    /// 개체 타입에 맞춰 기본 FSM 상태로 강제 복귀
    /// </summary>
    protected virtual void ResetFSMToDefaultState()
    {
        if (_stateMachine != null)
        {
            // 크리처는 포복 순찰(Patrol), 일반 적은 대기(Idle)로 진입
            int defaultStateKey = (this is CreatureEnemyController) ? (int)UnitState.Patrol : (int)UnitState.Idle;
            _stateMachine.ChangeState(defaultStateKey, forceReenter: true);
        }
    }
    #endregion

    public virtual void SetTarget(Transform target) => _currentTarget = target;

    public virtual void ClearTarget() => _currentTarget = null;

    #region Movement API
    public void StopMovementAPI() => Movement.StopMovement();
    public void SetDestinationAPI(Vector3 targetPosition, float speed = 4.5f) => Movement.SetDestination(targetPosition, speed);
    public void TickLocomotionRotationAPI(float deltaTime, float speed = 4.5f) => Movement.TickLocomotionRotation(deltaTime, speed);
    public void SetAgentableAPI(bool isEnable) => Movement.Agent.enabled = isEnable;
    public void FaceTargetAPI(Vector3 targetPosition, float rotationSpeed, float deltaTime)
        => Movement.FaceTarget(targetPosition, rotationSpeed, deltaTime);
    public void SetStoppingDistanceAPI(UnitState state)
        => Movement.SetStoppingDistance(state == UnitState.Chase ? _chaseStoppingDistance : _patrolStoppingDistance);
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
        float baseAtk = StatusController.EnemyStatusSource != null
            ? StatusController.EnemyStatusSource.AttackPower : 20.0f;

        float basePoise = StatusController.EnemyStatusSource != null
            ? StatusController.EnemyStatusSource.BasePoiseDamage : 15.0f;

        return new EnemyAttackContext(gameObject, transform, _enemyMovement,
            _enemyAnimController, _mainHitbox, baseAtk, basePoise);
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

        StatusController.TakeDamage(context.Damage, context.PoiseDamage);

        // Player 자동 타겟 지정
        if (_currentTarget == null && context.Attacker != null)
        {
            SetTarget(context.Attacker.transform);
        }

        if (StatusController.CurrentHp <= 0f)
        {
            _stateMachine.ChangeState((int)UnitState.Die);
            return;
        }

        // 2. 포이즈 0 도달 시 그로기/스턴 상태 전이
        if (StatusController.IsPoiseBroken)
        {
            _stateMachine.ChangeState((int)UnitState.Stun, true);
            return;
        }

        // 3. 일반 피격: 스턴 중이 아닐 때는 포이즈와 상관없이 즉시 GetHit 경직 출력
        if (_tier == EnemyTier.Normal && !(_stateMachine.CurrentState is EnemyStunState))
        {
            _stateMachine.ChangeState((int)UnitState.GetHit, true);
        }
    }
    #endregion

    public void OnParried(float parryPoiseDamage = 35.0f)
    {
        if (_stateMachine.CurrentState is EnemyDieState) return;

        // 보스는 패링당해도 행동을 취소하지 않고 패턴을 그대로 진행
        if (_tier == EnemyTier.Boss)
        {
            StatusController.TakeDamage(0f, 10.0f); // 보스는 미세 포이즈만 차감
            return;
        }

        // 1. 공격 즉시 강제 취소
        EnemyAttackContext context = CreateAttackContext();
        Combat.StopAttack(in context, 4f);

        // 2. 패링 포이즈 대미지 차감
        StatusController.TakeDamage(0f, parryPoiseDamage);

        // 3. 포이즈가 0이 되었다면 그로기 스턴 (앞잡 허용)
        if (StatusController.IsPoiseBroken)
        {
            _stateMachine.ChangeState((int)UnitState.Stun, true);
        }
        else
        {
            // 4. 포이즈가 남아있다면 앞잡 불가한 단순 패링 경직(ParryStagger) 진입
            _stateMachine.ChangeState((int)UnitState.ParryStagger, true);
        }
    }

    /// <summary>
    /// Player에게 Riposte를 당할 때 조작권 동기화 메서드
    /// </summary>
    public void OnRiposted(Transform attacker)
    {
        StopMovementAPI();
        CanBeRiposted = false;

        // 공격 및 비동기 루프 즉시 취소
        EnemyAttackContext context = CreateAttackContext();
        Combat.StopAttack(in context, 4f);

        // 강경직 애니메이션 재생 및 회전 동기화
        _enemyAnimController.PlayState(AnimStateType.Hit2);
        FaceTargetAPI(attacker.position, 20.0f, Time.deltaTime);

        // 스턴 상태일 경우 타이머를 멈추고 제어권 대기
        if (_stateMachine.CurrentState is EnemyStunState stunState)
        {
            stunState.LockForRiposte();
        }
    }


    protected virtual void OnDestroy()
    {
        // 씬에서 완전히 파괴될 때 매니저 목록에서 제거
        if (ServiceLocator.TryGet<IEnemyManager>(out IEnemyManager enemyManager))
        {
            enemyManager.UnregisterEnemy(this);
        }

        if (_perceptionModule != null)
        {
            _perceptionModule.OnTargetSpotted -= HandleTargetSpotted;
            _perceptionModule.OnNoiseHeard -= HandleNoiseHeard;
            _perceptionModule.Dispose();
        }
        _combatModule?.Dispose();
    }

    protected virtual void OnDrawGizmosSelected()
    {
        // 1. 물리 지면 접지 감지 기즈모
        Movement?.DrawGizmos();

        // 2. 시야 및 청각 센서 기즈모
        _perceptionModule?.DrawGizmos(transform);

        // 3. 스폰 위치 및 최대 추적 한계선(Leash Radius) 렌더링
        Vector3 spawn = Application.isPlaying ? _spawnPoint : transform.position;
        float maxLeash = (StatusController != null && StatusController.EnemyStatusSource != null)
            ? StatusController.EnemyStatusSource.MaxLeashDistance : 25.0f;

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
            float combatRange = (StatusController != null && StatusController.EnemyStatusSource != null)
                ? StatusController.EnemyStatusSource.AttackRange : 2.0f;

#if UNITY_EDITOR
            Handles.color = new Color(1.0f, 0.1f, 0.1f, 0.5f);
            Handles.DrawWireDisc(transform.position, Vector3.up, combatRange);
#endif
        }
    }
}
