using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;
using Util = DebugLogger;

/// <summary>
/// 일반 에너미 표준 BT 컨트롤러
/// 기본 패턴: 순찰 -> 추격 -> 공격 -> 복귀 / 피격, 사망
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyAnimController), typeof(EnemyStatusController))]
public class EnemyBTController : UnitController, IDamageable, IPoolable, IRiposteable
{
    #region SerializeField
    [Header("Hitbox Socket Registry")]
    [SerializeField] private List<HitboxMapping> _hitboxMappings = new List<HitboxMapping>();
    protected readonly Dictionary<HitboxSlot, WeaponSweepHitbox> _hitboxMap = new Dictionary<HitboxSlot, WeaponSweepHitbox>();

    [Header("Groggy & Riposte Settings")]
    [Tooltip("그로기 상태에서 처형당하거나 깨어날 때까지의 회복 대기 시간")]
    [SerializeField] protected float _groggyRecoveryDuration = 8.0f;

    [Header("Spawn Settings (풀링 소환 대기)")]
    [SerializeField] protected float _spawnWaitDuration = 3.0f;

    [Header("Riposte Alignment Socket")]
    [SerializeField] private Transform _riposteSocket;

    [System.Serializable]
    public struct HitboxMapping
    {
        public HitboxSlot Slot;
        public WeaponSweepHitbox Hitbox;
    }
    #endregion

    #region 도메인 컴포넌트
    protected EnemyStatusController _enemyStatus;
    protected EnemyAnimController _enemyAnim;
    protected NavMeshAgent _agent;
    protected EnemyMovement _movement;
    protected EnemyPerceptionModule _perceptionModule;
    private EnemyFloatingHUDPresenter _hudPresenter;
    #endregion

    #region 도메인 프로퍼티
    public override UnitStatusController BaseStatusController => _enemyStatus;
    public override UnitAnimController BaseAnimController => _enemyAnim;

    public EnemyStatusController StatusController => _enemyStatus;
    public EnemyAnimController AnimController => _enemyAnim;
    public EnemyMovement Movement => _movement;
    public Transform CurrentTarget => _currentTarget;
    public Transform RiposteSocket => _riposteSocket != null ? _riposteSocket : transform;
    public Vector3 SpawnPoint => _spawnPoint;
    public bool IsDead => _enemyStatus != null && _enemyStatus.IsDead;
    public bool CanBeRiposted { get; protected set; } = false;
    #endregion

    #region 변수
    protected Transform _currentTarget;
    protected Vector3 _spawnPoint;
    protected Vector3 _patrolTarget;
    protected bool _isPatrolWaiting = false;
    protected bool _isSpawnedFromPool = false;
    private bool _isInitializing = false;
    protected bool _isDeathExecuted = false;
    private bool _isInvestigatingNoise = false;
    private Vector3 _investigateTargetPosition;

    // UnitCombatDatabaseSO 기반 평타 공격 캐시
    protected readonly Dictionary<AnimStateType, EnemyAttackData> _basicAttacks = new Dictionary<AnimStateType, EnemyAttackData>();
    protected readonly Dictionary<AnimStateType, float> _attackCooldowns = new Dictionary<AnimStateType, float>();
    protected readonly List<EnemyAttackData> _validAttackCandidates = new List<EnemyAttackData>();
    private readonly Collider[] _targetAcquireBuffer = new Collider[8];
    #endregion

    #region BT 구동 및 비동기 수명주기
    protected INode _rootNode;
    protected CancellationTokenSource _actionCts;
    protected CancellationTokenSource _groggyCts;
    protected bool _isActionExecuting = false;
    protected bool _isGroggy = false;
    protected float _globalAttackCooldownTime = 0f;
    #endregion

    private Vector3 _lastDestination = Vector3.positiveInfinity;
    private float _nextPathUpdateTime = 0f;

    protected override void SetupComponents()
    {
        _agent = GetComponent<NavMeshAgent>();
        _enemyStatus = GetComponent<EnemyStatusController>();
        _enemyAnim = GetComponent<EnemyAnimController>();
        _hudPresenter = GetComponent<EnemyFloatingHUDPresenter>();

        _movement = new EnemyMovement(_rigidbody, transform, _agent, _groundLayer, _groundCheckConfig);
        _spawnPoint = transform.position;

        // 지각 모듈 안전 기동 (장애물 레이어 분리 주입)
        _perceptionModule = new EnemyPerceptionModule(transform, _targetLayer, _groundLayer);
        _perceptionModule.OnTargetSpotted += HandleTargetSpotted;
        _perceptionModule.OnNoiseHeard += HandleNoiseHeard;

        // 다중 히트박스 소켓 레지스트리 구축
        BuildHitboxRegistry();

        if (_enemyStatus != null)
        {
            _enemyStatus.OnStatusDataChanged -= HandleStatusDataChanged;
            _enemyStatus.OnStatusDataChanged += HandleStatusDataChanged;
        }

        // Scene 사전 배치 몬스터 -> 전역 씬 준비 완료 이벤트 구독
        EventBusSystem.Subscribe<GameSceneReadyEvent>(HandleGameSceneReady);
    }

    public override void Init()
    {
        base.Init();
        if (_isInitializing) return;
        EnsureDataAndInitializeAsync(this.destroyCancellationToken).Forget();
    }

    /// <summary>
    /// GameSceneBootstrapper의 셋업이 완전히 끝났을 때만 BT와 센서 루프를 안전 기동
    /// </summary>
    private void HandleGameSceneReady(GameSceneReadyEvent evt)
    {
        if (_isSpawnedFromPool) return;

        Init();
        _perceptionModule?.StartSensorLoop();
    }

    #region SO 데이터 주입 파이프라인
    /// <summary>
    /// DataManager 비동기 완료 대기 후 안전하게 스탯과 공격 DB를 바인딩
    /// </summary>
    private async UniTaskVoid EnsureDataAndInitializeAsync(CancellationToken ct)
    {
        _isInitializing = true;
        try
        {
            if (DataManager.Instance != null && !DataManager.Instance.IsDataLoaded)
            {
                await DataManager.Instance.EnsureDataLoadedAsync(ct);
            }

            BindStatusData();
            BindCombatDatabase();

            if (_hudPresenter != null)
            {
                _hudPresenter.AllocateAndBindView();
            }

            _rootNode = BuildBehaviorTree();
            PickNextPatrolPoint();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    /// <summary>
    /// DataManager의 EnemyStatusSO로부터 스탯을 로드하여 런타임 스탯 컨트롤러 및 로코모션 수치 동기화
    /// </summary>
    public virtual void BindStatusData(EnemyInfo overrideStat = null)
    {
        EnemyInfo runtimeStatus = null;

        if (overrideStat != null)
        {
            runtimeStatus = overrideStat;
        }
        else if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<EnemyStatusSO>(out var statusSO))
        {
            if (statusSO.TryGetData(_unitId, out EnemyInfo baseStatus))
            {
                runtimeStatus = (EnemyInfo)baseStatus.Clone();
            }
        }

        if (runtimeStatus != null)
        {
            _enemyStatus.Init(runtimeStatus);

            if (_perceptionModule != null)
            {
                _perceptionModule.SightRadius = runtimeStatus.SightRadius;
                _perceptionModule.SightAngle = runtimeStatus.SightAngle;
                _perceptionModule.HearingRadius = runtimeStatus.HearingRadius;
            }
        }
    }

    /// <summary>
    /// UnitCombatDatabaseSO로부터 해당 몬스터의 기본 평타 공격 목록 적재
    /// </summary>
    protected virtual void BindCombatDatabase()
    {
        _basicAttacks.Clear();
        _attackCooldowns.Clear();

        if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<UnitCombatDatabaseSO>(out var combatDb))
        {
            if (combatDb.TryGetBasicAttacks(_unitId, out var attacks))
            {
                foreach (var kvp in attacks)
                {
                    _basicAttacks[kvp.Key] = kvp.Value;
                    _attackCooldowns[kvp.Key] = 0f;
                }
            }
        }
    }
    #endregion

    #region IPoolable 구현부
    /// <summary>
    /// 오브젝트 풀에서 스폰(소환)될 때 호출되는 초기화 진입점
    /// </summary>
    public virtual void OnSpawn()
    {
        _isSpawnedFromPool = true;
        _spawnPoint = transform.position;
        _isDeathExecuted = false;
        SetControllerActive(true);

        if (_collider != null) _collider.enabled = true;
        if (_agent != null)
        {
            _agent.Warp(transform.position);
            _agent.isStopped = false;
        }

        // 스탯 및 포이즈 복원
        _enemyStatus?.RestorePoiseToMax();
        _isGroggy = false;
        CanBeRiposted = false;
        ClearTarget();

        // BT 및 상태 초기화
        Init();
        _perceptionModule?.StartSensorLoop();

        // 풀 소환 전용 시퀀스 실행 (3초 대기 후 타깃 포착)
        ExecuteSpawnSequenceAsync(GetActionToken()).Forget();
    }

    public virtual void OnDespawn()
    {
        ReleaseTokenInternal();
        _hudPresenter?.ReleaseView();
        _isSpawnedFromPool = false;
        SetControllerActive(false);
        CloseAllHitboxes();
        _perceptionModule?.StopSensorLoop();
        AsyncUtil.CancelAndDispose(ref _actionCts);
        AsyncUtil.CancelAndDispose(ref _groggyCts);
        ClearTarget();
    }

    /// <summary>
    /// 일반 몬스터 소환 시퀀스: 3초간 제자리 대기 후 전투 돌입
    /// </summary>
    protected virtual async UniTaskVoid ExecuteSpawnSequenceAsync(CancellationToken token)
    {
        _isActionExecuting = true;
        _movement.StopMovement();
        _enemyAnim.PlayState(AnimStateType.Idle, force: true);

        try
        {
            await PlayStateAndWaitAsync(AnimStateType.Idle, token, force: true, _spawnWaitDuration);
        }
        catch (System.OperationCanceledException) { return; }
        finally
        {
            _isActionExecuting = false;
        }

        // 대기 종료 후 플레이어 탐색 후 전투 진입
        AcquireNearestTarget();
    }

    protected void AcquireNearestTarget()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, 25.0f, _targetAcquireBuffer, _targetLayer);
        for (int i = 0; i < hitCount; i++)
        {
            PlayerController player = _targetAcquireBuffer[i].GetComponentInParent<PlayerController>();
            if (player != null && !player.StatusController.IsDead)
            {
                SetTarget(player.transform);
                break;
            }
        }
        System.Array.Clear(_targetAcquireBuffer, 0, hitCount);
    }
    #endregion

    protected override void TickController()
    {
        if (_rootNode == null) return;
        _movement.CheckGrounded();
        _rootNode.Evaluate();
    }

    #region BT Build
    /// <summary>
    /// 표준 에너미 Behavior Tree 조립
    /// </summary>
    protected virtual INode BuildBehaviorTree()
    {
        var root = new SelectorNode();

        // 1. 사망 판정 노드
        var deathSeq = new SequenceNode();
        deathSeq.Add(new PredicateNode<EnemyBTController>(this, static enemy => enemy.IsDead));
        deathSeq.Add(new ActionNode(ExecuteDeath));
        root.Add(deathSeq);

        // 2. 그로기(Groggy) 브랜치
        var groggySeq = new SequenceNode();
        groggySeq.Add(new PredicateNode<EnemyBTController>(this, static enemy =>
            enemy._enemyStatus != null && enemy._enemyStatus.IsPoiseBroken && !enemy._isGroggy));
        groggySeq.Add(new ActionNode(TriggerGroggyAction));
        root.Add(groggySeq);

        // 3. 비동기 액션 진행 중 락
        root.Add(new PredicateNode<EnemyBTController>(this, static enemy => enemy._isActionExecuting || enemy._isGroggy));

        // 4. 소리 감지 의심/수색 브랜치 (복귀 중/평시 소리 반응)
        var investigateSeq = new SequenceNode();
        investigateSeq.Add(new PredicateNode<EnemyBTController>(this, static enemy => enemy._isInvestigatingNoise));
        investigateSeq.Add(new ActionNode(TickInvestigateNoise));
        root.Add(investigateSeq);

        // 5. 복귀 분기 (리쉬 초과 시)
        var returnSeq = new SequenceNode();
        returnSeq.Add(new PredicateNode<EnemyBTController>(this, static enemy => enemy.IsExceedingLeashDistance()));
        returnSeq.Add(new ActionNode(TickReturnToSpawn));
        root.Add(returnSeq);

        // 6. 전투 브랜치 (추격 및 공격)
        var combatBranch = new SequenceNode();
        combatBranch.Add(new PredicateNode<EnemyBTController>(this, static enemy => enemy._currentTarget != null));
        combatBranch.Add(BuildCombatSelector());
        root.Add(combatBranch);

        // 7. 기본 순찰 브랜치
        root.Add(new ActionNode(TickPatrol));

        return root;
    }

    protected virtual INode BuildCombatSelector()
    {
        var selector = new SelectorNode();

        // 사정거리 이내 & 쿨타임 충족: 다중 평타 공격 추첨 실행
        var attackSeq = new SequenceNode();
        attackSeq.Add(new PredicateNode<EnemyBTController>(this, static enemy =>
            Time.time >= enemy._globalAttackCooldownTime && enemy.HasAnyReadyAttack()));
        attackSeq.Add(new AsyncActionNode(ExecuteSelectedBasicAttackAsync, GetActionToken));
        selector.Add(attackSeq);

        // 사정거리 내에 있으나 쿨타임 대기 중일 때: 플레이어를 주시하며 대기 (추격하지 않고 멈춤)
        var combatWaitSeq = new SequenceNode();
        combatWaitSeq.Add(new PredicateNode<EnemyBTController>(this, static enemy => enemy.IsTargetWithinAnyAttackRange()));
        combatWaitSeq.Add(new ActionNode(TickCombatWait));
        selector.Add(combatWaitSeq);

        // 사정거리 밖: 타깃 추격
        var chaseSeq = new SequenceNode();
        chaseSeq.Add(new ActionNode(TickChaseTarget));
        selector.Add(chaseSeq);

        return selector;
    }
    #endregion

    #region AI 실행 서브 루틴
    public bool IsTargetWithinAnyAttackRange()
    {
        if (_currentTarget == null || _basicAttacks.Count == 0) return false;

        Vector3 diff = _currentTarget.position - transform.position;
        diff.y = 0f;
        float dist = diff.sqrMagnitude;

        foreach (var kvp in _basicAttacks)
        {
            if (dist <= (kvp.Value.AttackRange * kvp.Value.AttackRange)) return true;
        }
        return false;
    }

    protected bool HasAnyReadyAttack()
    {
        if (_currentTarget == null || _basicAttacks.Count == 0) return false;

        Vector3 diff = _currentTarget.position - transform.position;
        diff.y = 0f;
        float dist = diff.sqrMagnitude;

        foreach (var kvp in _basicAttacks)
        {
            if (dist <= (kvp.Value.AttackRange * kvp.Value.AttackRange) && Time.time >= _attackCooldowns[kvp.Key])
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 공격 사거리 내에서 쿨타임 대기 시 추격을 멈추고 대기하는 루틴
    /// </summary>
    protected virtual INode.ENodeState TickCombatWait()
    {
        if (_currentTarget == null) return INode.ENodeState.ENS_Failure;

        _movement.StopMovement();
        _movement.FaceTarget(_currentTarget.position, 6.0f, Time.deltaTime);
        _enemyAnim.PlayState(AnimStateType.Idle);

        return INode.ENodeState.ENS_Running;
    }

    protected virtual async UniTask<bool> ExecuteSelectedBasicAttackAsync(CancellationToken ct)
    {
        if (_currentTarget == null || IsDead) return false;

        // 1. 공격 티켓(Token) 획득 시도 (다굴 원천 방지)
        if (ServiceLocator.TryGet<IEnemyManager>(out var enemyManager) && 
            enemyManager is EnemyManager managerConcrete && !managerConcrete.TryAcquireAttackToken(this))
        {
            return false;
        }

        // 2. 유효 후보 수집
        _validAttackCandidates.Clear();
        Vector3 diff = _currentTarget.position - transform.position;
        diff.y = 0f;
        float dist = diff.sqrMagnitude;

        foreach (var kvp in _basicAttacks)
        {
            if (dist <= (kvp.Value.AttackRange * kvp.Value.AttackRange) && Time.time >= _attackCooldowns[kvp.Key])
            {
                _validAttackCandidates.Add(kvp.Value);
            }
        }

        if (_validAttackCandidates.Count == 0)
        {
            ReleaseTokenInternal();
            return false;
        }

        EnemyAttackData chosenData = _validAttackCandidates[Random.Range(0, _validAttackCandidates.Count)];

        _isActionExecuting = true;

        // 3. 확실한 물리 및 내비메시 이동 완전 정지
        _movement.StopMovement();

        try
        {
            // 4. 공격 선딜레이 와인드업 대기 (플레이어에게 회피/패링 준비 시간 부여)
            float elapsedWindup = 0f;
            while (elapsedWindup < chosenData.WindupDelay)
            {
                ct.ThrowIfCancellationRequested();
                if (_currentTarget != null)
                {
                    _movement.FaceTarget(_currentTarget.position, 12.0f, Time.deltaTime);
                }
                elapsedWindup += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // 5. 타임라인 러너 실행
            ActionTimelineStep[] steps = chosenData.GetTimelineSteps();
            float baseAtk = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.AttackPower : 20.0f;
            float basePoise = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.BasePoiseDamage : 15.0f;
            
            ActionTimelineRunner.ExecutionContext ctx = new ActionTimelineRunner.ExecutionContext
            {
                AnimController = _enemyAnim,
                Movement = _movement,
                Target = _currentTarget,
                BossOwner = null,
                GlobalSpeedMultiplier = 1.0f,
                OnOpenHitbox = OpenHitboxSlot,
                OnCloseHitbox = CloseHitboxSlot,
                OnCloseAllHitboxes = CloseAllHitboxes,
                BaseDamage = chosenData.CalculateFinalDamage(baseAtk),
                BasePoiseDamage = chosenData.CalculateFinalPoiseDamage(basePoise),
                IsParryable = chosenData.IsParryable,
                ImpactType = chosenData.ImpactType,
                KnockbackForce = chosenData.KnockbackForce
            };

            await ActionTimelineRunner.ExecuteTimelineAsync(steps, ctx, ct);
        }
        catch (System.OperationCanceledException)
        {
            return false;
        }
        finally
        {
            CloseAllHitboxes();
            ReleaseTokenInternal();
            _isActionExecuting = false;


            if (!IsDead)
            {
                // 공격 모션 종료 즉시 안전 복귀 (마네킹 프리징 해결)
                _enemyAnim.PlayState(AnimStateType.Idle, force: true);

                // 공격 완료 시점부터 개별 쿨타임 및 글로벌 쿨타임 기록
                _attackCooldowns[chosenData.AttackAnimStateType] = Time.time + chosenData.AttackCooldown;
                _globalAttackCooldownTime = Time.time + 1.2f; // 최소 1.2초간 다음 공격 금지
            }
        }

        return true;
    }

    private void ReleaseTokenInternal()
    {
        if (ServiceLocator.TryGet<IEnemyManager>(out var enemyManager) && enemyManager is EnemyManager managerConcrete)
        {
            managerConcrete.ReleaseAttackToken(this);
        }
    }

    protected virtual INode.ENodeState TickChaseTarget()
    {
        if (_currentTarget == null) return INode.ENodeState.ENS_Failure;

        float attackRange = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.AttackRange : 2.0f;
        // NavMeshAgent 멈춤 거리를 공격 사거리의 90%로 좁혀서 확실히 타격권 안으로 진입
        if (_agent != null)
        {
            _agent.stoppingDistance = Mathf.Max(1.0f, attackRange * 0.9f);
        }

        float currentChaseSpeed = _enemyStatus.EnemyMotionStatus != null ? _enemyStatus.EnemyMotionStatus.ChaseSpeed.Value : 4.5f;

        // 0.15초 주기 또는 타깃 위치 유의미 이동 시에만 네비메시 갱신
        if (Time.time >= _nextPathUpdateTime || (_currentTarget.position - _lastDestination).sqrMagnitude > 1.0f)
        {
            _lastDestination = _currentTarget.position;
            _nextPathUpdateTime = Time.time + 0.15f;
            _movement.SetDestination(_lastDestination, currentChaseSpeed);
        }

        return INode.ENodeState.ENS_Running;
    }

    protected virtual INode.ENodeState TickPatrol()
    {
        if (_isPatrolWaiting) return INode.ENodeState.ENS_Running;

        if (_agent != null)
        {
            _agent.stoppingDistance = 0.5f;
        }

        if (!_agent.pathPending && _agent.hasPath && _agent.remainingDistance <= _agent.stoppingDistance + 0.3f)
        {
            _isPatrolWaiting = true;
            _movement.StopMovement();
            _enemyAnim.PlayState(AnimStateType.PatrolIdle);

            WaitAndPickNextPointAsync(GetActionToken()).Forget();
        }
        else
        {
            _movement.TickLocomotionRotation(Time.deltaTime, 3.5f);
            _enemyAnim.PlayState(AnimStateType.Patrol);
        }

        return INode.ENodeState.ENS_Running;
    }

    private async UniTaskVoid WaitAndPickNextPointAsync(CancellationToken token)
    {
        bool canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(2.5f), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        PickNextPatrolPoint();
        _isPatrolWaiting = false;
    }

    protected virtual void PickNextPatrolPoint()
    {
        float maxLeash = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.MaxLeashDistance : 25.0f;
        float safePatrolRadius = maxLeash * 0.4f;

        bool foundValidPoint = false;
        Vector3 finalPatrolPoint = _spawnPoint;

        // NavmeshSurface 내에 좌표 3회 검사
        for (int i = 0; i < 3; i++)
        {
            Vector2 circle = Random.insideUnitCircle * safePatrolRadius;
            Vector3 randomCandidate = _spawnPoint + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(randomCandidate, out NavMeshHit hit, 3.5f, NavMesh.AllAreas))
            {
                finalPatrolPoint = hit.position;
                foundValidPoint = true;
                break;
            }
        }

        if (!foundValidPoint) finalPatrolPoint = _spawnPoint;

        _patrolTarget = finalPatrolPoint;

        float patrolSpeed = _enemyStatus.EnemyMotionStatus != null ? _enemyStatus.EnemyMotionStatus.PatrolSpeed.Value : 2.0f;

        _movement.SetDestination(_patrolTarget, patrolSpeed);
    }

    protected virtual INode.ENodeState ExecuteDeath()
    {
        if (_isDeathExecuted) return INode.ENodeState.ENS_Success;
        _isDeathExecuted = true;

        ReleaseTokenInternal();
        SetControllerActive(false);

        AsyncUtil.CancelAndDispose(ref _actionCts);
        AsyncUtil.CancelAndDispose(ref _groggyCts);

        _movement.StopMovement();
        CloseAllHitboxes();
        _perceptionModule?.StopSensorLoop();

        if (_collider != null) _collider.enabled = false;
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;

        _enemyAnim.PlayState(AnimStateType.Death, force: true);
        EventBusSystem.Publish(new UnitDiedEvent(gameObject, _unitId));

        if (this is BossBTController)
        {
            EventBusSystem.Publish(new BossDefeatedEvent(gameObject));
        }

        return INode.ENodeState.ENS_Success;
    }

    private async UniTaskVoid ExecuteDespawnAfterDeathAsync(CancellationToken token)
    {
        float deathClipDuration = _enemyAnim.GetStateDuration(AnimStateType.Death);
        float totalWait = deathClipDuration + 1.5f; // 사망 연출 후 1.5초간 시체 유지

        bool isCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(totalWait), 
            delayTiming: PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();

        if (isCanceled) return;

        // 풀링 소환 개체인 경우 오브젝트 풀로 회수
        if (_isSpawnedFromPool && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Despawn(PoolType.NightmareCreature, gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    #endregion

    #region 다중 히트박스 제어 API
    private void BuildHitboxRegistry()
    {
        _hitboxMap.Clear();

        for (int i = 0; i < _hitboxMappings.Count; i++)
        {
            if (_hitboxMappings[i].Hitbox != null && !_hitboxMap.ContainsKey(_hitboxMappings[i].Slot))
            {
                _hitboxMap.Add(_hitboxMappings[i].Slot, _hitboxMappings[i].Hitbox);
            }
        }

        // 기본 무기 소켓이 미등록된 경우 자식 객체에서 자동 폴백
        if (!_hitboxMap.ContainsKey(HitboxSlot.MainWeapon))
        {
            WeaponSweepHitbox defaultHitbox = GetComponentInChildren<WeaponSweepHitbox>();
            if (defaultHitbox != null)
            {
                _hitboxMap.Add(HitboxSlot.MainWeapon, defaultHitbox);
            }
        }
    }
    public void OpenHitboxSlot(HitboxSlot slot, float damage, float poiseDamage, bool isParryable,
        AttackImpactType impactType = AttackImpactType.Light, float knockbackForce = 0f)
    {
        if (_hitboxMap.TryGetValue(slot, out WeaponSweepHitbox hitbox) && hitbox != null)
        {
            hitbox?.OpenHitBox(gameObject, damage, poiseDamage, isParryable, impactType, knockbackForce);
        }
    }

    public void CloseHitboxSlot(HitboxSlot slot)
    {
        if (_hitboxMap.TryGetValue(slot, out WeaponSweepHitbox hitbox) && hitbox != null)
        {
            hitbox?.CloseHitBox();
        }
    }

    public void CloseAllHitboxes()
    {
        foreach (var hitbox in _hitboxMap.Values)
        {
            hitbox?.CloseHitBox();
        }
    }
    #endregion

    #region 패링 피격, 그로기 & 처형
    /// <summary>
    /// 플레이어 패링 성공 시 호출: 진행 중인 패턴을 취소하고 짧은 패링 경직(ParryStagger)을 수행
    /// </summary>
    public virtual void OnParried(float parryPoiseDamage = 35.0f)
    {
        if (IsDead) return;

        ReleaseTokenInternal();
        // 진행 중인 모든 공격/비동기 태스크 즉각 인터럽트 취소
        AsyncUtil.CancelAndDispose(ref _actionCts);
        CloseAllHitboxes();

        _enemyStatus.TakeDamage(0f, parryPoiseDamage);

        // 포이즈 파괴 시 그로기 돌입
        if (_enemyStatus.IsPoiseBroken)
        {
            StartGroggySequence();
        }
        else
        {
            // 앞잡 불가한 단순 단기 경직 (ParryStagger) 실행
            ExecuteParryStaggerAsync(GetActionToken()).Forget();
        }
    }

    protected virtual async UniTaskVoid ExecuteParryStaggerAsync(CancellationToken token)
    {
        _isActionExecuting = true;
        _movement.StopMovement();
        CanBeRiposted = false;

        try
        {
            // 패링 경직 3초
            await PlayStateAndWaitAsync(AnimStateType.Hit1, token, force: true, 3f);
        }
        catch (System.OperationCanceledException) { }
        finally
        {
            _enemyAnim.PlayState(AnimStateType.Idle, force: true);
            _isActionExecuting = false;
        }
    }

    protected virtual INode.ENodeState TriggerGroggyAction()
    {
        StartGroggySequence();
        return INode.ENodeState.ENS_Running;
    }

    /// <summary>
    /// 강인도 소진 시 그로기 상태 진입 및 회복 루틴 시작
    /// </summary>
    public void StartGroggySequence()
    {
        if (_isGroggy || IsDead) return;

        _isGroggy = true;
        CanBeRiposted = true;
        _isActionExecuting = false;

        // 그로기 진입 즉시 진행 중이던 공격 비동기 태스크 강제 취소 (그로기 중 공격 발동 차단)
        AsyncUtil.CancelAndDispose(ref _actionCts);

        _movement.StopMovement();
        CloseAllHitboxes();
        ReleaseTokenInternal();

        _enemyStatus?.SetPoiseRecoveryPause(true);

        AsyncUtil.CancelAndDispose(ref _groggyCts);
        _groggyCts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);

        ExecuteGroggyTimerAsync(_groggyCts.Token).Forget();
    }

    /// <summary>
    /// 처형당하지 않을 경우 설정된 시간 후 스스로 기상하는 비동기 루틴
    /// </summary>
    private async UniTaskVoid ExecuteGroggyTimerAsync(CancellationToken token)
    {
        float elapsed = 0f;
        float maxPoise = _enemyStatus.MaxPoise;

        try
        {
            _enemyAnim.PlayState(AnimStateType.Groggy_Loop, force: true);

            while (elapsed < _groggyRecoveryDuration)
            {
                bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (canceled) return;

                elapsed += Time.deltaTime;
                // 점진적 Poise 충전 (UI 반영)
                float currentPoise = Mathf.Lerp(0f, maxPoise, elapsed / _groggyRecoveryDuration);
                // StatusController의 Poise를 점진 갱신
            }
        }
        catch (System.OperationCanceledException) { return; }

        RecoverFromGroggyNaturally();
    }

    private void RecoverFromGroggyNaturally()
    {
        CanBeRiposted = false;
        _isGroggy = false;
        _enemyStatus?.RestorePoiseToMax();
        _enemyStatus?.SetPoiseRecoveryPause(false);
        _enemyAnim.PlayState(AnimStateType.Idle, force: true);
    }

    /// <summary>
    /// 플레이어 처형(앞잡) 피격 시 호출
    /// </summary>
    public virtual void OnRiposted(Transform attacker)
    {
        ReleaseTokenInternal();
        _movement.StopMovement();
        CanBeRiposted = false;
        CloseAllHitboxes();

        // 기존 그로기 자동 회복 타이머 취소
        AsyncUtil.CancelAndDispose(ref _groggyCts);

        // 플레이어 정면으로 즉각 시선 스냅
        if (attacker != null)
        {
            Vector3 dirToAttacker = attacker.position - transform.position;
            dirToAttacker.y = 0f;
            if (dirToAttacker.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dirToAttacker.normalized);
            }
        }

        // 3. 처형 피격 애니메이션 강제 재생
        _enemyAnim.PlayState(AnimStateType.Hit2, force: true);
    }

    /// <summary>
    /// 플레이어 처형 모션이 완료된 직후 호출: 기상 모션 후 강인도 복원 및 전투 재개
    /// </summary>
    public virtual void OnRiposteFinished()
    {
        if (IsDead) return;
        ExecuteRiposteWakeUpAsync(GetActionToken()).Forget();
    }

    /// <summary>
    /// 처형 종료 후 바로 Max 충전이 아닌 회복 지연 후 기상
    /// </summary>
    protected virtual async UniTaskVoid ExecuteRiposteWakeUpAsync(CancellationToken token)
    {
        _isActionExecuting = true;
        CanBeRiposted = false;

        try
        {
            await PlayStateAndWaitAsync(AnimStateType.Hit2, token, force: true, customDuration: 2.5f);
        }
        catch (System.OperationCanceledException) { }
        finally
        {
            // 기상 완료 후 Poise 완전 복원 및 전투 재개
            _enemyStatus?.RestorePoiseToMax();
            _enemyStatus?.SetPoiseRecoveryPause(false);
            _isActionExecuting = false;
            _isGroggy = false;
            CanBeRiposted = false;
            _enemyAnim.PlayState(AnimStateType.Idle, force: true);
        }
    }
    #endregion

    #region 유틸리티 및 센싱
    protected virtual void HandleTargetSpotted(Transform target)
    {
        if (IsDead) return;
        if (_currentTarget == null)
        {
            SetTarget(target);
        }
    }

    protected virtual void HandleNoiseHeard(Vector3 noisePosition) 
    {
        if (_currentTarget != null || _isActionExecuting || IsDead) return;

        _investigateTargetPosition = noisePosition;
        _isInvestigatingNoise = true;
    }

    private INode.ENodeState TickInvestigateNoise()
    {
        _movement.StopMovement();
        _movement.FaceTarget(_investigateTargetPosition, 5.0f, Time.deltaTime);
        _enemyAnim.PlayState(AnimStateType.Idle);

        Vector3 dirToNoise = _investigateTargetPosition - transform.position;
        dirToNoise.y = 0f;

        // 소리 방향으로 거의 정렬되었을 때 1.5초간 둘러보고 수색 종료
        if (Vector3.Dot(transform.forward, dirToNoise.normalized) > 0.9f)
        {
            WaitAndEndInvestigateAsync(GetActionToken()).Forget();
        }

        return INode.ENodeState.ENS_Running;
    }
    private async UniTaskVoid WaitAndEndInvestigateAsync(CancellationToken token)
    {
        bool canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(1.5f), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;
        _isInvestigatingNoise = false;
    }
    #endregion

    #region 복귀 안정화 (스폰 지점 도착 시까지 보장)
    private bool _isReturning = false;

    protected bool IsReturningToSpawn()
    {
        if (_isReturning) return true;

        float maxLeash = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.MaxLeashDistance : 25.0f;
        Vector3 diff = transform.position - _spawnPoint;
        diff.y = 0f;

        if (diff.sqrMagnitude > (maxLeash * maxLeash))
        {
            _isReturning = true;
            ClearTarget();
            return true;
        }
        return false;
    }

    protected virtual INode.ENodeState TickReturnToSpawn()
    {
        _isInvestigatingNoise = false;
        ClearTarget();
        ReleaseTokenInternal();

        float walkSpeed = _enemyStatus.MotionStatus != null ? _enemyStatus.MotionStatus.WalkSpeed.Value : 3.5f;
        _movement.SetDestination(_spawnPoint, walkSpeed);
        _movement.TickLocomotionRotation(Time.deltaTime, 4.0f);
        _enemyAnim.PlayState(AnimStateType.Walk);

        Vector3 toSpawn = transform.position - _spawnPoint;
        toSpawn.y = 0f;
        if (toSpawn.sqrMagnitude <= (1.2f * 1.2f))
        {
            _movement.StopMovement();
            _isReturning = false;
            return INode.ENodeState.ENS_Success;
        }

        return INode.ENodeState.ENS_Running;
    }

    public virtual void SetTarget(Transform target) => _currentTarget = target;
    public virtual void ClearTarget() => _currentTarget = null;

    protected bool IsTargetInDistance(float range)
    {
        if (_currentTarget == null) return false;
        Vector3 diff = _currentTarget.position - transform.position;
        diff.y = 0f;
        return diff.sqrMagnitude <= (range * range);
    }

    protected bool IsExceedingLeashDistance()
    {
        float maxLeash = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.MaxLeashDistance : 25.0f;
        Vector3 diff = transform.position - _spawnPoint;
        diff.y = 0f;
        return diff.sqrMagnitude > (maxLeash * maxLeash);
    }

    public CancellationToken GetActionToken()
    {
        AsyncUtil.CancelAndDispose(ref _actionCts);
        _actionCts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);
        return _actionCts.Token;
    }
    #endregion

    #region IDamageableTest 구현부
    public virtual void ApplyDamage(in DamageContext context)
    {
        if (IsDead) return;

        // 몬스터의 고정 방어력(FlatDefense)에 따른 실 대미지 관통 연산
        float def = (_enemyStatus != null && _enemyStatus.EnemyInfoSource != null)
            ? _enemyStatus.EnemyInfoSource.FlatDefense : 10.0f;

        float penetratedDamage = CombatFormula.CalculatePenetratedDamage(context.Damage, def);

        _enemyStatus.TakeDamage(penetratedDamage, context.PoiseDamage);

        if (_currentTarget == null && context.Attacker != null)
        {
            SetTarget(context.Attacker.transform);
        }

        if (IsDead)
        {
            ExecuteDeath();
            return;
        }

        // Poise 파괴 시 즉시 그로기 상태로 전이
        if (_enemyStatus.IsPoiseBroken && !_isGroggy)
        {
            StartGroggySequence();
            return;
        }

        // 일반 피격 경직 (스턴 상태가 아닐 때만)
        if (!_isGroggy && (context.ImpactType == AttackImpactType.Light || context.ImpactType == AttackImpactType.Heavy))
        {
            ExecuteGetHitAsync(GetActionToken()).Forget();
        }
    }

    protected virtual async UniTaskVoid ExecuteGetHitAsync(CancellationToken token)
    {
        ReleaseTokenInternal();
        _isActionExecuting = true;
        _movement.StopMovement();
        CloseAllHitboxes();

        try
        {
            // Hit 모션이 짧은 경우도 생각해야함
            await PlayStateAndWaitAsync(AnimStateType.Hit1, token, force: true);
        }
        catch (System.OperationCanceledException) { }
        finally
        {
            _enemyAnim.PlayState(AnimStateType.Idle, force: true);
            _isActionExecuting = false;
        }
    }
    #endregion

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (Application.isPlaying && _movement != null)
        {
            _movement.UpdateGroundConfig(_groundCheckConfig);
        }
    }
#endif

    /// <summary>
    /// 인스펙터에서 RuntimeEnemyInfo 값을 조절했을 때 지각 센서 및 기즈모 수치를 실시간 갱신
    /// </summary>
    private void HandleStatusDataChanged(EnemyInfo updatedInfo)
    {
        if (updatedInfo == null) return;

        if (_perceptionModule != null)
        {
            _perceptionModule.SightRadius = updatedInfo.SightRadius;
            _perceptionModule.SightAngle = updatedInfo.SightAngle;
            _perceptionModule.HearingRadius = updatedInfo.HearingRadius;
        }

        // 씬 뷰 기즈모 재렌더링 유도
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    protected virtual void OnDestroy()
    {
        EventBusSystem.Unsubscribe<GameSceneReadyEvent>(HandleGameSceneReady);

        if (ServiceLocator.TryGet<IEnemyManager>(out var enemyManager))
        {
            enemyManager.UnregisterEnemy(this);
        }

        if (_enemyStatus != null)
        {
            _enemyStatus.OnStatusDataChanged -= HandleStatusDataChanged;
        }
        _perceptionModule?.Dispose();
        AsyncUtil.CancelAndDispose(ref _actionCts);
        AsyncUtil.CancelAndDispose(ref _groggyCts);
    }
}