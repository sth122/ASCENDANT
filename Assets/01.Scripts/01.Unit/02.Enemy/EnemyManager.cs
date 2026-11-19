using System.Collections.Generic;
using UnityEngine;
using Util = DebugLogger<EnemyManager>;

/// <summary>
/// 씬에 존재하는 모든 Enemy의 인스턴스를 관리하고,
/// 화톳불 휴식(Bonfire Rest) 시 3단계 분기 규칙에 맞춰 월드 리셋을 총괄하는 컴포넌트
/// </summary>
public class EnemyManager : MonoBehaviour, IEnemyManager
{
    // 현재 씬에 등록된 모든 에너미 인스턴스 목록 (비활성화 상태 포함)
    private readonly List<EnemyBTController> _registeredEnemies = new List<EnemyBTController>(64);

    // 이미 처치되어 리젠되면 안 되는 보스/네임드 몬스터의 고유 ID 목록
    private readonly HashSet<string> _defeatedPermanentIds = new HashSet<string>();

    [Header("다대일 전투 토큰 시스템 (동시 공격 허용 수량)")]
    [SerializeField] private int _maxSimultaneousAttackTokens = 3;

    public int MaxSimultaneousAttackTokens => _maxSimultaneousAttackTokens;

    public int ActiveTokenCount
    {
        get
        {
            PruneInvalidTokenHolders();
            return _tokenHolders.Count;
        }
    }

    // 현재 공격 토큰을 점유 중인 적 집합
    private readonly HashSet<EnemyBTController> _tokenHolders = new HashSet<EnemyBTController>(16);

    // 외곽에서 대기 중인 적 목록 
    private readonly List<EnemyBTController> _waitingEnemies = new List<EnemyBTController>(32);

    // 유효성 검사 중 무효화된 토큰 보유자를 안전하게 제거하기 위한 임시 버퍼
    private readonly List<EnemyBTController> _staleTokenBuffer = new List<EnemyBTController>(8);

    private void Awake()
    {
        ServiceLocator.Register<IEnemyManager>(this, ServiceLifetime.Local);
        SubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        _registeredEnemies.Clear();
        _defeatedPermanentIds.Clear();
        _tokenHolders.Clear();
        _waitingEnemies.Clear();
        _staleTokenBuffer.Clear();
    }

    private void Start()
    {
        RegisterPreplacedEnemiesInScene();
    }

    /// <summary>
    /// 씬에 미리 배치된 모든 EnemyBTController(비활성 상태 포함)를 전수 스캔하여 누락 없이 등록
    /// </summary>
    public void RegisterPreplacedEnemiesInScene()
    {
        EnemyBTController[] sceneEnemies = FindObjectsByType<EnemyBTController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < sceneEnemies.Length; i++)
        {
            RegisterEnemy(sceneEnemies[i]);
        }
    }

    #region 이벤트 구독
    private void SubscribeEvents()
    {
        UnsubscribeEvents();
        EventBusSystem.Subscribe<BonfireRestedEvent>(HandleBonfireRested);
        EventBusSystem.Subscribe<UnitDiedEvent>(HandleUnitDied);
    }

    private void UnsubscribeEvents()
    {
        EventBusSystem.Unsubscribe<BonfireRestedEvent>(HandleBonfireRested);
        EventBusSystem.Unsubscribe<UnitDiedEvent>(HandleUnitDied);
    }

    private void HandleBonfireRested(BonfireRestedEvent eventData)
    {
        Util.Log($"[화톳불 상호작용 감지] {eventData.BonfirePosition}");
        ResetAllEnemies();
    }

    private void HandleUnitDied(UnitDiedEvent evt)
    {
        if (evt.DeadUnit != null && evt.DeadUnit.TryGetComponent<PlayerController>(out _)) return;

        EnemyBTController deadEnemy = null;
        if (evt.DeadUnit != null && evt.DeadUnit.TryGetComponent(out deadEnemy))
        {
            ReleaseAttackToken(deadEnemy);
        }
        else if (evt.Identifier == UnitId.Player)
        {
            return;
        }

        int dropSouls = ResolveDropSouls(deadEnemy, evt.Identifier);
        if (dropSouls <= 0)
        {
            Util.LogWarning($"[소울 지급 생략] 대상: {(evt.DeadUnit != null ? evt.DeadUnit.name : evt.Identifier.ToString())}, DropSouls가 0 이하입니다.");
            return;
        }

        if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned && provider.Player != null)
        {
            provider.Player.StatusController.AddSouls(dropSouls);
            Util.Log($"[소울 획득] {(evt.DeadUnit != null ? evt.DeadUnit.name : evt.Identifier.ToString())} 처치 보상: +{dropSouls} Souls");
        }
    }

    private int ResolveDropSouls(EnemyBTController deadEnemy, UnitId unitId)
    {
        // 1. 사망한 적 인스턴스의 런타임 EnemyStatusController 수치 최우선 사용
        if (deadEnemy != null && deadEnemy.StatusController != null && deadEnemy.StatusController.DropSouls > 0)
        {
            return deadEnemy.StatusController.DropSouls;
        }

        // 2. 폴백: DataManager의 BossInfoSO / EnemyInfoSO 조회
        if (DataManager.Instance == null || unitId == UnitId.Player) return 0;

        if (DataManager.Instance.TryGetSingle<BossInfoSO>(out var bossSO) &&
            bossSO.TryGetData(unitId, out var bossInfo) && bossInfo.DropSouls > 0)
        {
            return bossInfo.DropSouls;
        }

        if (DataManager.Instance.TryGetSingle<EnemyInfoSO>(out var enemySO) &&
            enemySO.TryGetData(unitId, out var enemyInfo) && enemyInfo.DropSouls > 0)
        {
            return enemyInfo.DropSouls;
        }

        return 0;
    }
    #endregion

    #region 공격 시스템
    /// <summary>
    /// 해당 적이 현재 공격 우선권(토큰)을 보유하고 있는지 확인
    /// </summary>
    public bool HasAttackToken(EnemyBTController enemy)
    {
        if (enemy == null) return false;
        PruneInvalidTokenHolders(); 
        return _tokenHolders.Contains(enemy);
    }

    /// <summary>
    /// 적 AI가 플레이어를 처음 인식했을 때 호출하여 발견 순서대로 공격 토큰 발급.
    /// 최대 허용 수량을 초과한 경우 발견 순서를 유지하며 대기열에 등록.
    /// </summary>
    public bool TryAcquireAttackToken(EnemyBTController enemy)
    {
        if (!IsEligibleForToken(enemy)) return false;

        PruneInvalidTokenHolders();

        // 이미 토큰을 보유 중이라면 우선권 유지
        if (_tokenHolders.Contains(enemy))
        {
            return true;
        }

        // 여유 토큰이 존재하면 선착순으로 즉시 발급
        if (_tokenHolders.Count < _maxSimultaneousAttackTokens)
        {
            _waitingEnemies.Remove(enemy);
            _tokenHolders.Add(enemy);
            return true;
        }

        // 정원이 가득 찬 경우, 발견 순서를 보존하며 대기열 끝에 등록
        if (!_waitingEnemies.Contains(enemy))
        {
            _waitingEnemies.Add(enemy);
        }

        return false;
    }

    /// <summary>
    /// 토큰 보유자가 사망, 최대 이동 범위 이탈, 비활성화될 때 호출.
    /// 토큰을 회수하고 대기열의 다음 적에게 즉시 공격 토큰을 이동.
    /// </summary>
    public void ReleaseAttackToken(EnemyBTController enemy)
    {
        if (enemy == null) return;

        _waitingEnemies.Remove(enemy);

        // 실제로 토큰을 점유하고 있던 개체가 반납한 경우에만 빈 자리를 다음 대기 적에게 승계
        if (_tokenHolders.Remove(enemy))
        {
            PromoteNextWaitingEnemy();
        }
    }

    /// <summary>
    /// 빈 토큰 슬롯이 발생했을 때, 플레이어를 인식 중인 대기 적(선착순)에게 즉시 토큰 승계
    /// </summary>
    private void PromoteNextWaitingEnemy()
    {
        // 1. 발견 순서대로 등록된 대기열에서 유효한 적을 우선 승계
        while (_tokenHolders.Count < _maxSimultaneousAttackTokens && _waitingEnemies.Count > 0)
        {
            EnemyBTController nextCandidate = _waitingEnemies[0];
            _waitingEnemies.RemoveAt(0);

            if (IsEligibleForToken(nextCandidate) && !_tokenHolders.Contains(nextCandidate))
            {
                _tokenHolders.Add(nextCandidate);
            }
        }

        // 2. 대기열이 비었음에도 토큰 자리가 남았다면 씬 내 타겟을 인식 중인 다른 적을 탐색해 보충
        if (_tokenHolders.Count < _maxSimultaneousAttackTokens)
        {
            for (int i = 0; i < _registeredEnemies.Count; i++)
            {
                EnemyBTController candidate = _registeredEnemies[i];
                if (IsEligibleForToken(candidate) && !_tokenHolders.Contains(candidate))
                {
                    _tokenHolders.Add(candidate);
                    if (_tokenHolders.Count >= _maxSimultaneousAttackTokens) break;
                }
            }
        }
    }

    /// <summary>
    /// 적이 공격 토큰을 보유하거나 승계받을 수 있는 유효 상태인지 검증
    /// </summary>
    private bool IsEligibleForToken(EnemyBTController enemy)
    {
        return enemy != null && enemy.gameObject.activeInHierarchy && !enemy.IsDead && enemy.CurrentTarget != null;
    }

    /// <summary>
    /// 사망·비활성화·그로기·타임아웃 상태의 적이 점유 중인 유령 토큰을 자동 회수
    /// </summary>
    private void PruneInvalidTokenHolders()
    {
        if (_tokenHolders.Count == 0) return;

        _staleTokenBuffer.Clear();

        foreach (var holder in _tokenHolders)
        {
            if (!IsEligibleForToken(holder))
            {
                _staleTokenBuffer.Add(holder);
            }
        }

        if (_staleTokenBuffer.Count > 0)
        {
            for (int i = 0; i < _staleTokenBuffer.Count; i++)
            {
                _tokenHolders.Remove(_staleTokenBuffer[i]);
            }
            _staleTokenBuffer.Clear();

            // 회수된 수만큼 대기 중인 적에게 즉시 토큰 승계
            PromoteNextWaitingEnemy();
        }
    }

    /// <summary>
    /// 이미 공격 사거리(attackRange) 안에 도달한 전열의 대기 적이 있을 때,
    /// 사거리 바깥(attackRange + 1.5m 초과)에서 인파에 막혀 접근하지 못하는 토큰 보유자와 권한을 즉시 스왑
    /// </summary>
    public bool TrySwapWithBlockedHolder(EnemyBTController frontEnemy, float attackRange)
    {
        if (!IsEligibleForToken(frontEnemy)) return false;
        if (_tokenHolders.Contains(frontEnemy)) return true;

        // 여유 토큰이 남아있다면 즉시 발급
        if (_tokenHolders.Count < _maxSimultaneousAttackTokens)
        {
            _waitingEnemies.Remove(frontEnemy);
            _tokenHolders.Add(frontEnemy);
            return true;
        }

        // 토큰 보유자 중 현재 공격 중이 아니면서 플레이어와 가장 멀리 떨어져(길막 상태) 있는 개체 탐색
        EnemyBTController blockedHolder = null;
        float maxBlockedDistSqr = (attackRange + 1.5f) * (attackRange + 1.5f);

        foreach (var holder in _tokenHolders)
        {
            if (holder == null || holder == frontEnemy) continue;
            if (holder.IsActionExecuting || holder.CurrentTarget == null) continue;

            Vector3 diff = holder.CurrentTarget.position - holder.transform.position;
            diff.y = 0f;

            if (diff.sqrMagnitude > maxBlockedDistSqr)
            {
                maxBlockedDistSqr = diff.sqrMagnitude;
                blockedHolder = holder;
            }
        }

        if (blockedHolder != null)
        {
            _tokenHolders.Remove(blockedHolder);
            if (!_waitingEnemies.Contains(blockedHolder))
            {
                _waitingEnemies.Add(blockedHolder);
            }

            _waitingEnemies.Remove(frontEnemy);
            _tokenHolders.Add(frontEnemy);
            return true;
        }

        return false;
    }
    #endregion

    #region IEnemyManager 구현부
    /// <summary>
    /// 에너미 스폰 또는 씬 초기화 시 관리 목록에 등록
    /// </summary>
    public void RegisterEnemy(EnemyBTController enemy)
    {
        if (enemy != null && !_registeredEnemies.Contains(enemy))
        {
            _registeredEnemies.Add(enemy);
        }
    }

    /// <summary>
    /// 에너미가 완전히 파괴(Destroy)될 때 관리 목록에서 제외
    /// </summary>
    public void UnregisterEnemy(EnemyBTController enemy)
    {
        if (enemy != null)
        {
            ReleaseAttackToken(enemy);
            _registeredEnemies.Remove(enemy);
        }
    }

    /// <summary>
    /// 보스나 네임드 적 처치 시 영구 사망 플래그에 등록
    /// </summary>
    public void RegisterDefeatedPermanentEnemy(string permanentId)
    {
        if (string.IsNullOrEmpty(permanentId)) return;

        if (_defeatedPermanentIds.Add(permanentId))
        {
            Util.Log($"[영구 처치 등록] 고유 ID: {permanentId} (화톳불 리셋 대상에서 영구 제외)");
        }
    }

    /// <summary>
    /// 해당 고유 ID를 가진 적이 이미 처치되었는지 검증
    /// </summary>
    public bool IsPermanentEnemyDefeated(string permanentId)
    {
        if (string.IsNullOrEmpty(permanentId)) return false;
        return _defeatedPermanentIds.Contains(permanentId);
    }

    /// <summary>
    /// 화톳불 휴식 시 등록된 모든 에너미에게 리셋을 명령하고 토큰 및 대기열을 초기화
    /// </summary>
    public void ResetAllEnemies()
    {
        _tokenHolders.Clear();
        _waitingEnemies.Clear();
        _staleTokenBuffer.Clear();

        for (int i = _registeredEnemies.Count - 1; i >= 0; i--)
        {
            EnemyBTController enemy = _registeredEnemies[i];
            if (enemy == null)
            {
                _registeredEnemies.RemoveAt(i);
                continue;
            }

            if (IsPermanentEnemyDefeated(enemy.name))
            {
                enemy.gameObject.SetActive(false);
                continue;
            }

            enemy.gameObject.SetActive(true);

            if (enemy.Collider != null)
            {
                enemy.Collider.enabled = true;
            }

            enemy.transform.position = enemy.SpawnPoint;
            if (enemy.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent))
            {
                if (agent.isOnNavMesh)
                {
                    agent.Warp(enemy.SpawnPoint);
                }
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            if (enemy.AnimController != null)
            {
                enemy.AnimController.ResetStateCache();
                enemy.AnimController.PlayState(AnimStateType.Idle, force: true);
            }
            enemy.ClearTarget();
            enemy.SetControllerActive(true);
            enemy.Init();
        }
        Util.Log($"총 {_registeredEnemies.Count}기의 적 화톳불 리셋 프로세스 완료");
    }
    #endregion
}