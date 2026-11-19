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
    [SerializeField] private int _maxSimultaneousAttackTokens = 3; // 일반 전투 룸 기본 3기만 공격 허용
    [SerializeField] private float _maxTokenHoldDuration = 6.0f;

    public int MaxSimultaneousAttackTokens => _maxSimultaneousAttackTokens;
    public bool HasAvailableAttackToken => ActiveTokenCount < _maxSimultaneousAttackTokens;

    public int ActiveTokenCount
    {
        get
        {
            PruneInvalidTokenHolders();
            return _tokenHolders.Count;
        }
    }

    private readonly Dictionary<EnemyBTController, float> _tokenHolders = new Dictionary<EnemyBTController, float>(16);
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
    public bool HasAttackToken(EnemyBTController enemy)
    {
        if (enemy == null) return false;
        PruneInvalidTokenHolders();
        return _tokenHolders.ContainsKey(enemy);
    }

    /// <summary>
    /// 적 AI가 공격을 시작하기 전 공격 권한 티켓 요청
    /// </summary>
    public bool TryAcquireAttackToken(EnemyBTController enemy)
    {
        if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy) return false;

        PruneInvalidTokenHolders();

        if (_tokenHolders.ContainsKey(enemy))
        {
            return true;
        }

        if (_tokenHolders.Count < _maxSimultaneousAttackTokens)
        {
            _tokenHolders.Add(enemy, Time.time);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 공격 모션 종료, 피격, 그로기, 사망 시 티켓 반환
    /// </summary>
    public void ReleaseAttackToken(EnemyBTController enemy)
    {
        if (enemy != null)
        {
            _tokenHolders.Remove(enemy);
        }
    }

    /// <summary>
    /// 사망·비활성화·그로기·타임아웃 상태의 적이 점유 중인 유령 토큰을 자동 회수
    /// </summary>
    private void PruneInvalidTokenHolders()
    {
        if (_tokenHolders.Count == 0) return;

        _staleTokenBuffer.Clear();
        float now = Time.time;

        foreach (var kvp in _tokenHolders)
        {
            EnemyBTController holder = kvp.Key;
            float acquiredTime = kvp.Value;

            if (holder == null ||
                !holder.gameObject.activeInHierarchy ||
                holder.IsDead ||
                holder.ActionState == EnemyActionState.Groggy ||
                (now - acquiredTime) > _maxTokenHoldDuration)
            {
                _staleTokenBuffer.Add(holder);
            }
        }

        for (int i = 0; i < _staleTokenBuffer.Count; i++)
        {
            _tokenHolders.Remove(_staleTokenBuffer[i]);
        }
        _staleTokenBuffer.Clear();
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
    /// 화톳불 휴식 시 등록된 모든 에너미에게 리셋을 명령
    /// </summary>
    public void ResetAllEnemies()
    {
        _tokenHolders.Clear();

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