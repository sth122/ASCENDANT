using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Util = DebugLogger;

/// <summary>
/// 일반 에너미 표준 BT 컨트롤러
/// 기본 패턴: 순찰 -> 추격 -> 공격 -> 복귀 / 피격, 사망
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyAnimController), typeof(EnemyStatusController))]
public class EnemyBTController : UnitController, IDamageableTest, IPoolable
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

    [Serializable]
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

    // UnitCombatDatabaseSO 기반 평타 공격 캐시
    protected readonly Dictionary<AnimStateType, EnemyAttackData> _basicAttacks = new Dictionary<AnimStateType, EnemyAttackData>();
    protected readonly Dictionary<AnimStateType, float> _attackCooldowns = new Dictionary<AnimStateType, float>();
    protected readonly List<EnemyAttackData> _validAttackCandidates = new List<EnemyAttackData>();
    #endregion

    #region BT 구동 및 비동기 수명주기
    protected INode _rootNode;
    protected CancellationTokenSource _actionCts;
    protected bool _isActionExecuting = false;
    protected bool _isGroggy = false;
    protected float _globalAttackCooldownTime = 0f;
    #endregion

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
        _perceptionModule.StartSensorLoop();

        // 다중 히트박스 소켓 레지스트리 구축
        BuildHitboxRegistry();

        if (_enemyStatus != null)
        {
            _enemyStatus.OnStatusDataChanged -= HandleStatusDataChanged;
            _enemyStatus.OnStatusDataChanged += HandleStatusDataChanged;
        }
    }

    protected override void Start()
    {
        // 씬에 정적으로 배치된 경우 즉시 자가 기동
        if (!_isSpawnedFromPool)
        {
            base.Start();
        }
    }

    public override void Init()
    {
        base.Init();
        EnsureDataAndInitializeAsync(this.destroyCancellationToken).Forget();
    }

    #region SO 데이터 주입 파이프라인
    /// <summary>
    /// DataManager 비동기 완료 대기 후 안전하게 스탯과 공격 DB를 바인딩
    /// </summary>
    private async UniTaskVoid EnsureDataAndInitializeAsync(CancellationToken ct)
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

            Util.LogWithTag(name, $"[{_unitId}] EnemyStatusSO 주입 완료");
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
                Util.LogWithTag(name, $"[{_unitId}] 기본 평타 {attacks.Count}종 적재 완료");
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
        SetControllerActive(true);

        if (_collider != null) _collider.enabled = true;
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = false;

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
        _hudPresenter?.ReleaseView();
        _isSpawnedFromPool = false;
        SetControllerActive(false);
        CloseAllHitboxes();
        _perceptionModule?.StopSensorLoop();
        AsyncUtil.CancelAndDispose(ref _actionCts);
        ClearTarget();
    }

    /// <summary>
    /// 일반 몬스터 소환 시퀀스: 3초간 제자리 대기 후 전투 돌입
    /// </summary>
    protected virtual async UniTaskVoid ExecuteSpawnSequenceAsync(CancellationToken token)
    {
        _isActionExecuting = true;
        _movement.StopMovement();
        _enemyAnim.PlayState(AnimStateType.BattleIdle, force: true);

        try
        {
            await PlayStateAndWaitAsync(AnimStateType.BattleIdle, token, force: true, _spawnWaitDuration);
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            _isActionExecuting = false;
        }

        // 대기 종료 후 플레이어 탐색 후 전투 진입
        AcquireNearestTarget();
    }

    protected void AcquireNearestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 25.0f, _targetLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerController player = hits[i].GetComponentInParent<PlayerController>();
            if (player != null && !player.StatusController.IsDead)
            {
                SetTarget(player.transform);
                break;
            }
        }
    }
    #endregion

    protected override void TickController()
    {
        if (IsDead || _rootNode == null) return;
        _movement.CheckGrounded();
        _rootNode.Evaluate();
    }

    /// <summary>
    /// 표준 에너미 Behavior Tree 조립
    /// </summary>
    protected virtual INode BuildBehaviorTree()
    {
        var root = new SelectorNode();

        // 1. 사망 판정 노드
        var deathSeq = new SequenceNode();
        deathSeq.Add(new PredicateNode<EnemyBTController>(this, static (enemy) => enemy.IsDead));
        deathSeq.Add(new ActionNode(ExecuteDeath));
        root.Add(deathSeq);

        // 2. 그로기(Stun) 브랜치
        var groggySeq = new SequenceNode();
        groggySeq.Add(new PredicateNode<EnemyBTController>(this, static (enemy) =>
            enemy._enemyStatus != null && enemy._enemyStatus.IsPoiseBroken && !enemy._isGroggy));
        groggySeq.Add(new AsyncActionNode(ExecuteGroggyAsync, GetActionToken));
        root.Add(groggySeq);

        // 3. 비동기 액션 진행 중 락
        root.Add(new PredicateNode<EnemyBTController>(this, static (enemy) => enemy._isActionExecuting));

        // 4. 복귀 분기 (리쉬 초과 시)
        var returnSeq = new SequenceNode();
        returnSeq.Add(new PredicateNode<EnemyBTController>(this, static (enemy) => enemy.IsExceedingLeashDistance()));
        returnSeq.Add(new ActionNode(TickReturnToSpawn));
        root.Add(returnSeq);

        // 5. 전투 브랜치 (추격 및 공격)
        var combatBranch = new SequenceNode();
        combatBranch.Add(new PredicateNode<EnemyBTController>(this, static (enemy) => enemy._currentTarget != null));
        combatBranch.Add(BuildCombatSelector());
        root.Add(combatBranch);

        // 6. 기본 순찰 브랜치
        root.Add(new ActionNode(TickPatrol));

        return root;
    }

    protected virtual INode BuildCombatSelector()
    {
        var selector = new SelectorNode();

        // 사정거리 이내 & 쿨타임 충족: 다중 평타 공격 추첨 실행
        var attackSeq = new SequenceNode();
        attackSeq.Add(new PredicateNode<EnemyBTController>(this, static (enemy) =>
            Time.time >= enemy._globalAttackCooldownTime && enemy.HasAnyReadyAttack()));
        attackSeq.Add(new AsyncActionNode(ExecuteSelectedBasicAttackAsync, GetActionToken));
        selector.Add(attackSeq);

        // 사정거리 밖: 타깃 추격
        var chaseSeq = new SequenceNode();
        chaseSeq.Add(new ActionNode(TickChaseTarget));
        selector.Add(chaseSeq);

        return selector;
    }

    #region AI 실행 서브 루틴
    protected bool HasAnyReadyAttack()
    {
        if (_currentTarget == null || _basicAttacks.Count == 0) return false;

        Vector3 diff = _currentTarget.position - transform.position;
        diff.y = 0f;
        float centerDistance = diff.magnitude;

        // 두 캐릭터 캡슐 콜라이더의 외곽선 간격 (콜라이더 반지름 합산 1.0m 차감 보정)
        float effectiveDistance = Mathf.Max(0f, centerDistance - 1.0f);

        foreach (var kvp in _basicAttacks)
        {
            float maxRange = kvp.Value.AttackRange;
            if (effectiveDistance <= maxRange && Time.time >= _attackCooldowns[kvp.Key])
            {
                return true;
            }
        }
        return false;
    }

    protected virtual async UniTask<bool> ExecuteSelectedBasicAttackAsync(CancellationToken ct)
    {
        // 1. 유효 사거리 및 쿨타임 충족 후보 수집
        _validAttackCandidates.Clear();
        Vector3 diff = _currentTarget.position - transform.position;
        diff.y = 0f;
        float sqrDist = diff.sqrMagnitude;

        foreach (var kvp in _basicAttacks)
        {
            float range = kvp.Value.AttackRange;
            if (sqrDist <= (range * range) && Time.time >= _attackCooldowns[kvp.Key])
            {
                _validAttackCandidates.Add(kvp.Value);
            }
        }

        if (_validAttackCandidates.Count == 0) return false;

        // 2. 무작위 패턴 1종 선정
        EnemyAttackData chosenData = _validAttackCandidates[UnityEngine.Random.Range(0, _validAttackCandidates.Count)];

        _isActionExecuting = true;
        _movement.StopMovement();

        // ActionTimelineStep 배열 추출
        ActionTimelineStep[] steps = chosenData.GetTimelineSteps();

        float baseAtk = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.AttackPower : 20.0f;
        float basePoise = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.BasePoiseDamage : 15.0f;

        ActionTimelineRunner.ExecutionContext ctx = new ActionTimelineRunner.ExecutionContext
        {
            AnimController = _enemyAnim,
            Movement = _movement,
            Target = _currentTarget,
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

        try
        {
            // 타임라인 러너 실행
            await ActionTimelineRunner.ExecuteTimelineAsync(steps, ctx, ct);
        }
        finally
        {
            _isActionExecuting = false;
        }

        // 모션 완전 종료 후부터 쿨타임 타임스탬프 기록
        _attackCooldowns[chosenData.AttackAnimStateType] = Time.time + chosenData.AttackCooldown;
        _globalAttackCooldownTime = Time.time + 0.8f;

        return true;
    }

    protected virtual INode.ENodeState TickChaseTarget()
    {
        if (_currentTarget == null) return INode.ENodeState.ENS_Failure;

        float attackRange = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.AttackRange : 2.0f;
        // NavMeshAgent 멈춤 거리를 공격 사거리의 70%로 좁혀서 확실히 타격권 안으로 진입
        if (_agent != null)
        {
            _agent.stoppingDistance = Mathf.Max(0.8f, attackRange * 0.7f);
        }

        float currentChaseSpeed = _enemyStatus.EnemyMotionStatus != null ? _enemyStatus.EnemyMotionStatus.ChaseSpeed.Value : 4.5f;

        _movement.SetDestination(_currentTarget.position, currentChaseSpeed);
        _movement.TickLocomotionRotation(Time.deltaTime, 4.5f);
        _enemyAnim.PlayState(AnimStateType.Run);

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
        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(2.5f), cancellationToken: token).SuppressCancellationThrow();
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
            Vector2 circle = UnityEngine.Random.insideUnitCircle * safePatrolRadius;
            Vector3 randomCandidate = _spawnPoint + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(randomCandidate, out NavMeshHit hit, 3.5f, NavMesh.AllAreas))
            {
                finalPatrolPoint = hit.position;
                foundValidPoint = true;
                break;
            }
        }

        if (!foundValidPoint)
        {
            finalPatrolPoint = _spawnPoint;
        }

        _patrolTarget = finalPatrolPoint;

        float patrolSpeed = _enemyStatus.EnemyMotionStatus != null ? _enemyStatus.EnemyMotionStatus.PatrolSpeed.Value : 2.0f;

        _movement.SetDestination(_patrolTarget, patrolSpeed);
    }

    protected virtual INode.ENodeState TickReturnToSpawn()
    {
        ClearTarget();

        float walkSpeed = _enemyStatus.MotionStatus != null ? _enemyStatus.MotionStatus.WalkSpeed.Value : 3.5f;

        _movement.SetDestination(_spawnPoint, walkSpeed);
        _movement.TickLocomotionRotation(Time.deltaTime, 4.0f);
        _enemyAnim.PlayState(AnimStateType.Walk);

        Vector3 toSpawn = transform.position - _spawnPoint;
        toSpawn.y = 0f;
        if (toSpawn.sqrMagnitude <= (1.2f * 1.2f))
        {
            _movement.StopMovement();
            return INode.ENodeState.ENS_Success;
        }

        return INode.ENodeState.ENS_Running;
    }

    protected virtual INode.ENodeState ExecuteDeath()
    {
        SetControllerActive(false);
        _movement.StopMovement();
        CloseAllHitboxes();

        if (_collider != null) _collider.enabled = false;
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;

        _enemyAnim.PlayState(AnimStateType.Death, force: true);
        EventBusSystem.Publish(new UnitDiedEvent(gameObject, _unitId));

        return INode.ENodeState.ENS_Success;
    }
    #endregion

    #region 다중 히트박스 제어 API
    private void BuildHitboxRegistry()
    {
        _hitboxMap.Clear();

        for (int i = 0; i < _hitboxMappings.Count; i++)
        {
            if (_hitboxMappings[i].Hitbox != null && _hitboxMap.ContainsKey(_hitboxMappings[i].Slot))
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
            hitbox.OpenHitBox(gameObject, damage, poiseDamage, isParryable, impactType, knockbackForce);
        }
    }

    public void CloseHitboxSlot(HitboxSlot slot)
    {
        if (_hitboxMap.TryGetValue(slot, out WeaponSweepHitbox hitbox) && hitbox != null)
        {
            hitbox.CloseHitBox();
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

        // 진행 중인 모든 공격/비동기 태스크 즉각 인터럽트 취소
        AsyncUtil.CancelAndDispose(ref _actionCts);
        CloseAllHitboxes();

        _enemyStatus.TakeDamage(0f, parryPoiseDamage);

        // 포이즈 파괴 시 그로기 돌입
        if (_enemyStatus.IsPoiseBroken)
        {
            ExecuteGroggyAsync(GetActionToken()).Forget();
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
        catch (OperationCanceledException) { }
        finally
        {
            _isActionExecuting = false;
        }
    }

    /// <summary>
    /// 포이즈 고갈에 따른 그로기 무력화 루틴
    /// </summary>
    protected virtual async UniTask<bool> ExecuteGroggyAsync(CancellationToken ct)
    {
        _isActionExecuting = true;
        _isGroggy = true;
        CanBeRiposted = true; // 앞잡 처형 허용
        _movement.StopMovement();
        CloseAllHitboxes();

        Util.LogWithTag(name, "[그로기 무력화 발생] 앞잡 가능 상태 진입");

        try
        {
            await PlayStateAndWaitAsync(AnimStateType.Stun, ct, force: true, customDuration: _groggyRecoveryDuration);
        }
        catch (OperationCanceledException)
        {
            // 플레이어 처형(Riposte)에 의해 인터럽트된 경우
            return false;
        }
        finally
        {
            CanBeRiposted = false;
            _isGroggy = false;
            _isActionExecuting = false;
            _enemyStatus.RestorePoiseToMax();
            _enemyAnim.PlayState(AnimStateType.Idle, force: true);
        }

        return true;
    }

    /// <summary>
    /// 플레이어 처형(앞잡) 피격 시 호출
    /// </summary>
    public virtual void OnRiposted(Transform attacker)
    {
        _movement.StopMovement();
        CanBeRiposted = false;
        CloseAllHitboxes();

        // 기존 그로기 자동 회복 타이머 취소
        AsyncUtil.CancelAndDispose(ref _actionCts);

        // 플레이어 정면으로 즉각 시선 스냅
        Vector3 dirToAttacker = (attacker.position - transform.position);
        dirToAttacker.y = 0f;
        if (dirToAttacker.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dirToAttacker.normalized);
        }

        _enemyAnim.PlayState(AnimStateType.Hit2, force: true);
        ExecuteRiposteWakeUpAsync(GetActionToken()).Forget();
    }

    /// <summary>
    /// 처형 종료 후 바로 Max 충전이 아닌 회복 지연 후 기상
    /// </summary>
    protected virtual async UniTaskVoid ExecuteRiposteWakeUpAsync(CancellationToken token)
    {
        _isActionExecuting = true;
        try
        {
            // 처형 모션 및 기상 대기
            await PlayStateAndWaitAsync(AnimStateType.Hit2, token, force: true, 3f);
        }
        catch (OperationCanceledException) { }
        finally
        {
            // 처형 완료 후 포이즈 재설정 및 복귀
            _enemyStatus.RestorePoiseToMax();
            _isActionExecuting = false;
            _isGroggy = false;
        }
    }
    #endregion

    #region 유틸리티 및 센싱
    protected virtual void HandleTargetSpotted(Transform target)
    {
        if (_currentTarget == null)
        {
            SetTarget(target);
        }
    }

    protected virtual void HandleNoiseHeard(Vector3 noisePosition) 
    {
        if (_currentTarget != null || _isActionExecuting || IsDead) return;

        Vector3 dirToNoise = noisePosition - transform.position;
        dirToNoise.y = 0f;

        if (dirToNoise.sqrMagnitude > 0.01f)
        {
            _movement.FaceTarget(noisePosition, 6.0f, Time.deltaTime);
        }
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

        _enemyStatus.TakeDamage(context.Damage, context.PoiseDamage);

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
            ExecuteGroggyAsync(GetActionToken()).Forget();
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
        _isActionExecuting = true;
        _movement.StopMovement();
        CloseAllHitboxes();

        try
        {
            // Hit 모션이 짧은 경우도 생각해야함
            await PlayStateAndWaitAsync(AnimStateType.Hit1, token, force: true);
        }
        catch (OperationCanceledException) { }
        finally
        {
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
    #region 기즈모 렌더링 (스폰 앵커와 지면 판정 구체 완전 분리)
    protected virtual void OnDrawGizmosSelected()
    {
        // 1. 몬스터 발밑을 정확히 따라다니는 지면 판정 구체 렌더링 (접지: 녹색, 공중: 적색)
        _movement?.DrawGizmos();

        // 2. 시야각 및 청각 반경 기즈모
        _perceptionModule?.DrawGizmos(transform);

        // 3. 스폰 지점 마커 (구체가 아닌 납작한 앵커 디스크로 렌더링하여 혼동 방지)
        Vector3 spawn = Application.isPlaying ? _spawnPoint : transform.position;
        float maxLeash = (_enemyStatus != null && _enemyStatus.EnemyInfoSource != null)
            ? _enemyStatus.EnemyInfoSource.MaxLeashDistance : 25.0f;
        float safePatrolRadius = maxLeash * 0.4f;

        // 스폰 지점 앵커 마크: 중심 납작 디스크
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(spawn + Vector3.up * 0.05f, new Vector3(0.5f, 0.1f, 0.5f));

#if UNITY_EDITOR
        // 안전 순찰 반경 (청록색)
        Handles.color = new Color(0f, 0.9f, 0.8f, 0.2f);
        Handles.DrawSolidDisc(spawn, Vector3.up, safePatrolRadius);
        Handles.color = Color.cyan;
        Handles.DrawWireDisc(spawn, Vector3.up, safePatrolRadius);

        // 최대 활동 반경 Leash (녹색)
        Handles.color = new Color(0.1f, 1.0f, 0.3f, 0.15f);
        Handles.DrawSolidDisc(spawn, Vector3.up, maxLeash);
        Handles.color = Color.green;
        Handles.DrawWireDisc(spawn, Vector3.up, maxLeash);
#endif

        // 4. 현재 타깃 연결선 및 공격 가능 사거리 링
        if (_currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up * 1.0f, _currentTarget.position + Vector3.up * 1.0f);

            float combatRange = (_enemyStatus != null && _enemyStatus.EnemyInfoSource != null)
                ? _enemyStatus.EnemyInfoSource.AttackRange : 2.2f;

#if UNITY_EDITOR
            Handles.color = new Color(1.0f, 0.1f, 0.1f, 0.5f);
            Handles.DrawWireDisc(transform.position, Vector3.up, combatRange + 1.0f);
#endif
        }
    }
    #endregion

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
        if (_enemyStatus != null)
        {
            _enemyStatus.OnStatusDataChanged -= HandleStatusDataChanged;
        }
        _perceptionModule?.Dispose();
        AsyncUtil.CancelAndDispose(ref _actionCts);
    }
}