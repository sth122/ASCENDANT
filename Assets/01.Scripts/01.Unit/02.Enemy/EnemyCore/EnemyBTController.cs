using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// AI 의사결정(BT Evaluation)과 수명주기만 총괄하는 최상위 베이스 컨트롤러
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyAnimController), typeof(EnemyStatusController))]
[RequireComponent(typeof(EnemyCombatController))]
public class EnemyBTController : UnitController, IDamageable, IPoolable, IRiposteable
{
    #region Settings
    [Header("Groggy & Riposte")]
    [SerializeField] protected float _groggyRecoveryDuration = 8.0f;
    [SerializeField] private Transform _riposteSocket;

    [Header("Spawn Settings")]
    [SerializeField] protected float _spawnWaitDuration = 3.0f;
    [SerializeField] protected float _tokenlessStandoffMargin = 2.2f; // 공격 토큰 미보유 시 추가 대치 거리
    #endregion

    #region 도메인 컴포넌트 및 모듈
    protected EnemyStatusController _enemyStatus;
    protected EnemyAnimController _enemyAnim;
    protected EnemyCombatController _combatController;
    protected NavMeshAgent _agent;
    protected EnemyMovement _movement;
    protected EnemyPerceptionModule _perceptionModule;

    protected EnemyGroggyModule _groggyModule;
    protected EnemyPatrolModule _patrolModule;
    private EnemyFloatingHUDPresenter _hudPresenter;
    #endregion

    #region 프로퍼티
    public override UnitStatusController BaseStatusController => _enemyStatus;
    public override UnitAnimController BaseAnimController => _enemyAnim;
    public EnemyStatusController StatusController => _enemyStatus;
    public EnemyAnimController AnimController => _enemyAnim;
    public EnemyCombatController CombatController => _combatController;
    public EnemyMovement Movement => _movement;

    public Transform CurrentTarget => _currentTarget;
    public Transform RiposteSocket => _riposteSocket != null ? _riposteSocket : transform;
    public Vector3 SpawnPoint => _patrolModule != null ? _patrolModule.SpawnPoint : transform.position;
    public LayerMask TargetLayer => _targetLayer;
    public bool IsDead => _enemyStatus != null && _enemyStatus.IsDead;
    public bool CanBeRiposted => _groggyModule != null && _groggyModule.CanBeRiposted;

    public EnemyActionState ActionState { get; protected set; } = EnemyActionState.None;
    public bool IsActionExecuting => ActionState != EnemyActionState.None && ActionState != EnemyActionState.Dead;
    #endregion

    #region 런타임 변수
    protected Transform _currentTarget;
    protected bool _isSpawnedFromPool = false;
    private bool _isInitializing = false;
    protected bool _isDeathExecuted = false;

    private bool _isInvestigatingNoise = false;
    private Vector3 _investigateTargetPosition;

    private readonly Collider[] _targetAcquireBuffer = new Collider[8];
    private Vector3 _lastDestination = Vector3.positiveInfinity;
    private float _nextPathUpdateTime = 0f;
    private float _strafeSign = 1f;

    protected INode _rootNode;
    protected CancellationTokenSource _actionCts;
    #endregion

    protected override void SetupComponents()
    {
        _agent = GetComponent<NavMeshAgent>();
        _enemyStatus = GetComponent<EnemyStatusController>();
        _enemyAnim = GetComponent<EnemyAnimController>();
        _hudPresenter = GetComponent<EnemyFloatingHUDPresenter>();

        _combatController = GetComponent<EnemyCombatController>();
        _combatController.Init(this);

        _movement = new EnemyMovement(_rigidbody, transform, _agent, _groundLayer, _groundCheckConfig);

        _groggyModule = new EnemyGroggyModule(this, _enemyStatus, _enemyAnim, _movement, _groggyRecoveryDuration);
        _patrolModule = new EnemyPatrolModule(this, _agent, _movement, _enemyAnim);

        _perceptionModule = new EnemyPerceptionModule(transform, _targetLayer, _groundLayer);
        _perceptionModule.OnTargetSpotted += HandleTargetSpotted;
        _perceptionModule.OnNoiseHeard += HandleNoiseHeard;

        _strafeSign = (GetInstanceID() & 1) == 0 ? 1f : -1f;

        if (_enemyStatus != null)
        {
            _enemyStatus.OnStatusDataChanged -= HandleStatusDataChanged;
            _enemyStatus.OnStatusDataChanged += HandleStatusDataChanged;
            _enemyStatus.OnPoiseBreakEvent -= TriggerGroggy;
            _enemyStatus.OnPoiseBreakEvent += TriggerGroggy;
        }


        EventBusSystem.Subscribe<GameSceneReadyEvent>(HandleGameSceneReady);
    }
    protected override void Start()
    {
        base.Start();
        RegisterSelfToManager();
    }

    protected virtual void OnDisable()
    {
        ReleaseAttackToken();
    }

    private void RegisterSelfToManager()
    {
        if (ServiceLocator.TryGet<IEnemyManager>(out var enemyManager))
        {
            enemyManager.RegisterEnemy(this);
        }
    }

    public override void Init()
    {
        base.Init();
        _isDeathExecuted = false;
        _isInvestigatingNoise = false;
        SetActionState(EnemyActionState.None);

        RegisterSelfToManager();
        if (_isInitializing) return;
        EnsureDataAndInitializeAsync(this.destroyCancellationToken).Forget();
    }

    private void HandleGameSceneReady(GameSceneReadyEvent evt)
    {
        if (_isSpawnedFromPool) return;
        Init();
        _perceptionModule?.StartSensorLoop();
    }

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

            if (_hudPresenter != null) _hudPresenter.AllocateAndBindView();

            _rootNode = BuildBehaviorTree();
            float patrolSpd = _enemyStatus.EnemyMotionStatus != null ? _enemyStatus.EnemyMotionStatus.PatrolSpeed.Value : 2.0f;
            float maxLeash = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.MaxLeashDistance : 25.0f;
            _patrolModule.PickNextPatrolPoint(patrolSpd, maxLeash);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    public virtual void BindStatusData(EnemyInfo overrideStat = null)
    {
        EnemyInfo runtimeStatus = null;
        if (overrideStat != null) runtimeStatus = overrideStat;
        else if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<EnemyInfoSO>(out var statusSO))
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

    protected virtual void BindCombatDatabase()
    {
        if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<UnitCombatDatabaseSO>(out var combatDb))
        {
            if (combatDb.TryGetBasicAttacks(_unitId, out var attacks))
            {
                _combatController.BindBasicAttacks(attacks);
            }
        }
    }

    public void SetActionState(EnemyActionState newState) => ActionState = newState;

    #region IPoolable 구현부
    public virtual void OnSpawn()
    {
        _isSpawnedFromPool = true;
        _isDeathExecuted = false;
        _isInvestigatingNoise = false;
        SetActionState(EnemyActionState.None);
        SetControllerActive(true);
        RegisterSelfToManager();

        if (_collider != null) _collider.enabled = true;
        if (_agent != null)
        {
            _agent.Warp(transform.position);
            _agent.isStopped = false;
        }

        _patrolModule.SetSpawnPoint(transform.position);
        _groggyModule.ResetState();
        _enemyStatus?.ResetPoiseToDefault();
        ClearTarget();

        _rootNode?.Reset();
        Init();
        _perceptionModule?.StartSensorLoop();

        ExecuteSpawnSequenceAsync(GetActionToken()).Forget();
    }

    public virtual void OnDespawn()
    {
        ReleaseAttackToken();
        _hudPresenter?.ReleaseView();
        _isSpawnedFromPool = false;
        SetControllerActive(false);

        _rootNode?.Reset();
        _combatController.CloseAllHitboxes();
        _perceptionModule?.StopSensorLoop();
        _groggyModule.ResetState();
        _patrolModule.ResetState();
        CancelCurrentAction();
        ClearTarget();
        SetActionState(EnemyActionState.None);
    }

    protected virtual async UniTaskVoid ExecuteSpawnSequenceAsync(CancellationToken token)
    {
        SetActionState(EnemyActionState.Spawning);
        _movement.StopMovement();
        _enemyAnim.PlayState(AnimStateType.Idle, force: true);

        try
        {
            await PlayStateAndWaitAsync(AnimStateType.Idle, token, force: true, customDuration: _spawnWaitDuration);
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            if (ActionState == EnemyActionState.Spawning)
            {
                SetActionState(EnemyActionState.None);
            }
        }

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
        Array.Clear(_targetAcquireBuffer, 0, hitCount);
    }
    #endregion

    protected override void TickController()
    {
        if (_rootNode == null || (GameManager.Instance != null && GameManager.Instance.IsPaused)) return;
        _movement.CheckGrounded();
        _rootNode.Evaluate();
    }

    #region BT Assembly
    protected virtual INode BuildBehaviorTree()
    {
        var root = new SelectorNode();

        // 1. 사망 판정
        var deathSeq = new SequenceNode();
        deathSeq.Add(new PredicateNode<EnemyBTController>(this, static e => e.IsDead));
        deathSeq.Add(new ActionNode(ExecuteDeath));
        root.Add(deathSeq);

        // 2. 그로기 브랜치
        var groggySeq = new SequenceNode();
        groggySeq.Add(new PredicateNode<EnemyBTController>(this, static e =>
            e._groggyModule.IsGroggy || (e._enemyStatus != null && e._enemyStatus.IsPoiseBroken)));
        groggySeq.Add(new ActionNode(TickGroggyStatus));
        root.Add(groggySeq);

        // 3. 소리 감지 수색
        var investigateSeq = new SequenceNode();
        investigateSeq.Add(new PredicateNode<EnemyBTController>(this, static e => !e.IsActionExecuting && e._isInvestigatingNoise));
        investigateSeq.Add(new ActionNode(TickInvestigateNoise));
        root.Add(investigateSeq);

        // 4. 복귀 분기
        var returnSeq = new SequenceNode();
        returnSeq.Add(new PredicateNode<EnemyBTController>(this, static e =>
            !e.IsActionExecuting && e._patrolModule.CheckShouldReturn(e._enemyStatus.EnemyInfoSource != null ? e._enemyStatus.EnemyInfoSource.MaxLeashDistance : 25f)));
        returnSeq.Add(new ActionNode(TickReturnToSpawn));
        root.Add(returnSeq);

        // 5. 전투 브랜치
        var combatBranch = new SequenceNode();
        combatBranch.Add(new PredicateNode<EnemyBTController>(this, static e => e._currentTarget != null && !e._groggyModule.IsGroggy));
        combatBranch.Add(BuildCombatSelector());
        root.Add(combatBranch);

        // 6. 기본 순찰
        root.Add(new ActionNode(TickPatrol));

        return root;
    }

    protected virtual INode BuildCombatSelector()
    {
        var selector = new SelectorNode();

        // 1. 공격 실행 (사거리 내 + 글로벌 쿨타임 완료 + 공격 토큰 획득)
        var attackSeq = new SequenceNode();
        attackSeq.Add(new PredicateNode<EnemyBTController>(this, static e =>
            !e.IsActionExecuting && e._combatController.IsGlobalCooldownReady && e._combatController.HasAnyReadyAttack(e._currentTarget)));
        attackSeq.Add(new AsyncActionNode(ExecuteSelectedBasicAttackAsync, GetActionToken));
        selector.Add(attackSeq);

        // 2. 대치 대기 (공격 사거리 내이거나, 공격 토큰이 없어 외곽 대치 거리에 도달한 경우)
        var combatWaitSeq = new SequenceNode();
        combatWaitSeq.Add(new PredicateNode<EnemyBTController>(this, static e =>
            !e.IsActionExecuting && e.IsWithinCombatWaitZone()));
        combatWaitSeq.Add(new ActionNode(TickCombatWait));
        selector.Add(combatWaitSeq);

        // 3. 추격 (토큰 보유 시 근접, 미보유 시 외곽 대치선까지 추격)
        var chaseSeq = new SequenceNode();
        chaseSeq.Add(new PredicateNode<EnemyBTController>(this, static e => !e.IsActionExecuting));
        chaseSeq.Add(new ActionNode(TickChaseTarget));
        selector.Add(chaseSeq);

        return selector;
    }

    protected virtual INode.ENodeState TickGroggyStatus()
    {
        // 1. 그로기가 진행 중이면 Running 상태 유지
        if (_groggyModule.IsGroggy)
        {
            return INode.ENodeState.ENS_Running;
        }

        // 2. 그로기 모듈이 꺼져 있는데 포이즈가 깨져서 진입한 상태라면 그로기 개시
        if (_enemyStatus != null && _enemyStatus.IsPoiseBroken)
        {
            TriggerGroggy();
            return INode.ENodeState.ENS_Running;
        }

        // 3. 그로기 타이머가 끝나 IsGroggy가 false가 되었다면 완료
        return INode.ENodeState.ENS_Success;
    }
    #endregion

    #region AI 서브 루틴
    /// <summary>
    /// 토큰 보유자만  공격 우선 순위 보유
    /// </summary>
    /// <returns></returns>
    protected bool HasCombatAttackPermission()
    {
        if (this is BossBTController) return true;

        if (ServiceLocator.TryGet<IEnemyManager>(out var enemyManager) && enemyManager is EnemyManager mgr)
        {
            return mgr.HasAttackToken(this);
        }
        return true;
    }

    protected bool IsWithinCombatWaitZone()
    {
        if (_currentTarget == null) return false;

        float defaultMeleeRange = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.AttackRange : 2.0f;
        float engageRange = _combatController.GetEffectiveEngageRange(defaultMeleeRange);

        Vector3 diff = _currentTarget.position - transform.position;
        diff.y = 0f;
        float distSqr = diff.sqrMagnitude;

        // 1. 사용 가능한(또는 기본 근접) 공격 사거리 안에 들어온 경우 대치/공격 대기
        if (distSqr <= (engageRange * engageRange))
        {
            return true;
        }

        // 2. 공격 토큰이 없는 대기 적은 외곽 대치 거리(기본 근접 사거리 + 여유 마진) 도달 시 멈춤
        if (!HasCombatAttackPermission())
        {
            float standoffDist = defaultMeleeRange + _tokenlessStandoffMargin;
            return distSqr <= (standoffDist * standoffDist);
        }

        return false;
    }

    protected virtual INode.ENodeState TickCombatWait()
    {
        if (_currentTarget == null) return INode.ENodeState.ENS_Failure;

        bool canAttack = HasCombatAttackPermission();
        if (_agent != null)
        {
            _agent.avoidancePriority = canAttack ? 20 : 75;
        }

        _movement.StopMovement();
        _movement.FaceTarget(_currentTarget.position, 6.0f, Time.deltaTime);
        _enemyAnim.PlayState(AnimStateType.Idle);

        return INode.ENodeState.ENS_Running;
    }

    protected virtual async UniTask<bool> ExecuteSelectedBasicAttackAsync(CancellationToken ct)
    {
        if (_currentTarget == null || IsDead || IsActionExecuting) return false;

        // 1. 토큰 미보유 상태라도 이미 사거리 내 진입했다면, 길막으로 못 들어오는 후열 보유자와 토큰 스왑 시도
        if (!HasCombatAttackPermission())
        {
            float defaultMeleeRange = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.AttackRange : 2.0f;
            float engageRange = _combatController.GetEffectiveEngageRange(defaultMeleeRange);

            if (ServiceLocator.TryGet<IEnemyManager>(out var enemyManager) &&
                enemyManager is EnemyManager mgr && !mgr.TrySwapWithBlockedHolder(this, engageRange))
            {
                return false;
            }
        }

        // 2. 쿨타임/사거리 불충족 시에도 토큰을 반납하지 않고 유지한 채 대기
        if (!_combatController.TryPickValidAttack(_currentTarget, out EnemyAttackData chosenData))
        {
            return false;
        }

        SetActionState(EnemyActionState.BasicAttacking);
        _combatController.ReserveGlobalCooldown(chosenData.AttackCooldown);
        if (_agent != null) _agent.avoidancePriority = 20;
        _movement.StopMovement();

        try
        {
            float elapsedWindup = 0f;
            while (elapsedWindup < chosenData.WindupDelay)
            {
                ct.ThrowIfCancellationRequested();
                if (_currentTarget != null) _movement.FaceTarget(_currentTarget.position, 12.0f, Time.deltaTime);
                elapsedWindup += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            float baseAtk = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.AttackPower : 20.0f;
            float basePoise = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.BasePoiseDamage : 15.0f;

            await _combatController.ExecuteAttackTimelineAsync(chosenData, _currentTarget, baseAtk, basePoise, 1.0f, null, ct);
        }
        catch (OperationCanceledException) { return false; }
        finally
        {
            _combatController.CloseAllHitboxes();

            if (_agent != null) _agent.avoidancePriority = 20;

            if (!IsDead && ActionState == EnemyActionState.BasicAttacking)
            {
                _enemyAnim.PlayState(AnimStateType.Idle, force: true);
                _combatController.RecordAttackCooldown(chosenData.AttackAnimStateType, chosenData.AttackCooldown);
                SetActionState(EnemyActionState.None);
            }
        }

        return true;
    }

    public void ReleaseAttackToken()
    {
        if (ServiceLocator.TryGet<IEnemyManager>(out var enemyManager) && enemyManager is EnemyManager managerConcrete)
        {
            managerConcrete.ReleaseAttackToken(this);
        }
    }

    /// <summary>
    /// 추격 및 대치 BT 틱 검사
    /// </summary>
    /// <returns></returns>
    protected virtual INode.ENodeState TickChaseTarget()
    {
        if (_currentTarget == null) return INode.ENodeState.ENS_Failure;

        bool canAttack = HasCombatAttackPermission();
        float defaultMeleeRange = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.AttackRange : 2.0f;
        float engageRange = _combatController.GetEffectiveEngageRange(defaultMeleeRange);

        float desiredStop = canAttack ? Mathf.Max(1.2f, engageRange * 0.85f) : (defaultMeleeRange + _tokenlessStandoffMargin);

        if (_agent != null)
        {
            _agent.stoppingDistance = desiredStop;
            _agent.avoidancePriority = canAttack ? 20 : 75;
        }

        float currentChaseSpeed = _enemyStatus.EnemyMotionStatus != null ? _enemyStatus.EnemyMotionStatus.ChaseSpeed.Value : 4.5f;
        if (!canAttack) currentChaseSpeed *= 0.9f;

        Vector3 targetDest = _currentTarget.position;
        if (!canAttack)
        {
            Vector3 toEnemy = transform.position - _currentTarget.position;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude > 0.01f)
            {
                Vector3 sideDir = Vector3.Cross(Vector3.up, toEnemy.normalized) * _strafeSign;
                targetDest += sideDir * 1.2f;
            }
        }

        if (Time.time >= _nextPathUpdateTime || (targetDest - _lastDestination).sqrMagnitude > 0.8f)
        {
            _lastDestination = targetDest;
            _nextPathUpdateTime = Time.time + 0.15f;
            _movement.SetDestination(_lastDestination, currentChaseSpeed);
        }

        _movement.TickLocomotionRotation(Time.deltaTime, 5.0f);
        _enemyAnim.PlayState(AnimStateType.Run);

        return INode.ENodeState.ENS_Running;
    }

    /// <summary>
    /// 최대 이동 범위 초과 시 복귀. 토큰 반납 및 공격  토큰 변경
    /// </summary>
    /// <returns></returns>
    protected virtual INode.ENodeState TickReturnToSpawn()
    {
        ReleaseAttackToken();
        ClearTarget();
        _isInvestigatingNoise = false;
        float walkSpeed = _enemyStatus.MotionStatus != null ? _enemyStatus.MotionStatus.WalkSpeed.Value : 3.5f;
        return _patrolModule.TickReturn(walkSpeed);
    }

    protected virtual INode.ENodeState TickPatrol()
    {
        if (IsActionExecuting) return INode.ENodeState.ENS_Running;
        float patrolSpeed = _enemyStatus.EnemyMotionStatus != null ? _enemyStatus.EnemyMotionStatus.PatrolSpeed.Value : 2.0f;
        float maxLeash = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.MaxLeashDistance : 25.0f;
        return _patrolModule.TickPatrol(patrolSpeed, maxLeash);
    }

    protected virtual INode.ENodeState ExecuteDeath()
    {
        if (_isDeathExecuted) return INode.ENodeState.ENS_Success;
        _isDeathExecuted = true;
        SetActionState(EnemyActionState.Dead);

        ReleaseAttackToken();
        SetControllerActive(false);

        CancelCurrentAction();
        _rootNode?.Reset();
        _groggyModule.Dispose();
        _patrolModule.Dispose();

        _movement.StopMovement();
        _combatController.CloseAllHitboxes();
        _perceptionModule?.StopSensorLoop();

        if (_collider != null) _collider.enabled = false;
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;

        _enemyAnim.PlayState(AnimStateType.Death, force: true);
        EventBusSystem.Publish(new UnitDiedEvent(gameObject, _unitId));

        if (this is BossBTController)
        {
            EventBusSystem.Publish(new BossDefeatedEvent(gameObject));
        }

        ExecuteDespawnAfterDeathAsync(this.destroyCancellationToken).Forget();
        return INode.ENodeState.ENS_Success;
    }
    private async UniTaskVoid ExecuteDespawnAfterDeathAsync(CancellationToken token)
    {
        float deathClipDuration = _enemyAnim.GetStateDuration(AnimStateType.Death);
        float totalWait = deathClipDuration + 1.5f;

        // 1. 사망 모션의 75% 시점까지 대기
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(totalWait),
            delayTiming: PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        if (_isSpawnedFromPool && ObjectPoolManager.Instance != null)
        {
            gameObject.SetActive(false);
        }
    }
    #endregion

    #region IRiposteable & Groggy 연동
    public virtual void OnParried(float parryPoiseDamage = 35.0f)
    {
        if (IsDead) return;

        CancelCurrentAction();
        _combatController.CloseAllHitboxes();

        _enemyStatus.TakeDamage(0f, parryPoiseDamage);

        if (_enemyStatus.IsPoiseBroken)
        {
            _groggyModule.StartGroggy();
        }
        else
        {
            ExecuteParryStaggerAsync(GetActionToken()).Forget();
        }
    }

    protected virtual async UniTaskVoid ExecuteParryStaggerAsync(CancellationToken token)
    {
        SetActionState(EnemyActionState.HitStagger);
        _movement.StopMovement();

        try
        {
            await PlayStateAndWaitAsync(AnimStateType.Hit1, token, force: true, customDuration: 3f);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!IsDead && ActionState == EnemyActionState.HitStagger)
            {
                _enemyAnim.PlayState(AnimStateType.Idle, force: true);
                SetActionState(EnemyActionState.None);
            }
        }
    }

    public virtual void OnRiposted() => _groggyModule.HandleRiposted();

    /// <summary>
    /// [그로기 단일 진입점]: 어떤 행동(공격, 스킬, 이동) 중이든 원자적으로 즉시 중단하고 그로기 돌입
    /// </summary>
    public virtual void TriggerGroggy()
    {
        if (IsDead || _groggyModule.IsGroggy) return;

        // 1. 진행 중인 공격/스킬 비동기 태스크 즉시 취소 (UniTask CancellationToken 발생)
        CancelCurrentAction();

        // 2. 물리/이동 및 히트박스 회수
        _combatController.CloseAllHitboxes();
        _movement.StopMovement();

        // 3. 상태를 Groggy로 변경
        SetActionState(EnemyActionState.Groggy);

        // 4. 그로기 모듈 시작 (Groggy_Loop 애니메이션 및 8초 카운트다운 타이머 시작)
        _groggyModule.StartGroggy();
    }
    #endregion

    #region 센싱 및 피격
    protected virtual void HandleTargetSpotted(Transform target)
    {
        if (IsDead) return;
        if (_currentTarget == null) SetTarget(target);
    }

    protected virtual void HandleNoiseHeard(Vector3 noisePosition)
    {
        if (_currentTarget != null || IsActionExecuting || IsDead) return;
        _investigateTargetPosition = noisePosition;
        _isInvestigatingNoise = true;
    }

    private INode.ENodeState TickInvestigateNoise()
    {
        _movement.StopMovement();
        _movement.FaceTarget(_investigateTargetPosition, 5.0f, Time.deltaTime);
        _enemyAnim.PlayState(AnimStateType.Idle);

        Vector3 dir = _investigateTargetPosition - transform.position;
        dir.y = 0f;

        if (Vector3.Dot(transform.forward, dir.normalized) > 0.9f)
        {
            WaitAndEndInvestigateAsync(GetActionToken()).Forget();
        }

        return INode.ENodeState.ENS_Running;
    }

    private async UniTaskVoid WaitAndEndInvestigateAsync(CancellationToken token)
    {
        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;
        _isInvestigatingNoise = false;
    }

    /// <summary>
    /// 타겟 인식 시점에 공격 토큰 신청
    /// </summary>
    /// <param name="target"></param>
    public virtual void SetTarget(Transform target)
    {
        if (_currentTarget == target) return;
        _currentTarget = target;

        if (_currentTarget != null && !IsDead && !(this is BossBTController))
        {
            if (ServiceLocator.TryGet<IEnemyManager>(out var enemyManager) && enemyManager is EnemyManager mgr)
            {
                mgr.TryAcquireAttackToken(this);
            }
        }
        else if (_currentTarget == null)
        {
            ReleaseAttackToken();
        }
    }
    public virtual void ClearTarget()
    {
        _currentTarget = null;
        ReleaseAttackToken();
    }

    /// <summary>
    /// 비동기 액션 새로운 CTS 생성
    /// </summary>
    /// <returns></returns>
    public CancellationToken GetActionToken()
    {
        CancelCurrentAction();
        _actionCts = CancellationTokenSource.CreateLinkedTokenSource(this.destroyCancellationToken);
        return _actionCts.Token;
    }

    /// <summary>
    /// 현재 실행 중인 비동기 액션 CTS 취소
    /// </summary>
    public void CancelCurrentAction() => AsyncUtil.CancelAndDispose(ref _actionCts);

    public virtual void ApplyDamage(in DamageContext context)
    {
        if (IsDead) return;

        float def = (_enemyStatus != null && _enemyStatus.EnemyInfoSource != null) ? _enemyStatus.EnemyInfoSource.FlatDefense : 10.0f;
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

        if (_enemyStatus.IsPoiseBroken && !_groggyModule.IsGroggy)
        {
            TriggerGroggy();
            return;
        }

        if (!IsActionExecuting && !_groggyModule.IsGroggy &&
            (context.ImpactType == AttackImpactType.Light || context.ImpactType == AttackImpactType.Heavy))
        {
            ExecuteGetHitAsync(GetActionToken()).Forget();
        }
    }

    protected virtual async UniTaskVoid ExecuteGetHitAsync(CancellationToken token)
    {
        SetActionState(EnemyActionState.HitStagger);
        _movement.StopMovement();
        _combatController.CloseAllHitboxes();

        try
        {
            await PlayStateAndWaitAsync(AnimStateType.Hit1, token, force: true);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!IsDead && ActionState == EnemyActionState.HitStagger)
            {
                _enemyAnim.PlayState(AnimStateType.Idle, force: true);
                SetActionState(EnemyActionState.None);
            }
        }
    }
    #endregion

    private void HandleStatusDataChanged(EnemyInfo updatedInfo)
    {
        if (updatedInfo == null) return;
        if (_perceptionModule != null)
        {
            _perceptionModule.SightRadius = updatedInfo.SightRadius;
            _perceptionModule.SightAngle = updatedInfo.SightAngle;
            _perceptionModule.HearingRadius = updatedInfo.HearingRadius;
        }
    }

    #region Facade API
    public void OpenHitboxSlot(HitboxSlot slot, float damage, float poiseDamage, bool isParryable,
           AttackImpactType impactType = AttackImpactType.Light, float knockbackForce = 0f)
    {
        _combatController?.OpenHitbox(slot, damage, poiseDamage, isParryable, impactType, knockbackForce);
    }

    public void CloseHitboxSlot(HitboxSlot slot) => _combatController?.CloseHitbox(slot);
    public void CloseAllHitboxes() => _combatController?.CloseAllHitboxes();
    public Transform GetMouthHitboxTransform() => _combatController != null ? _combatController.MouthSocket : transform;
    #endregion

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
            _enemyStatus.OnPoiseBreakEvent -= TriggerGroggy;
        }

        _perceptionModule?.Dispose();
        _groggyModule?.Dispose();
        _patrolModule?.Dispose();
        CancelCurrentAction();
    }
}