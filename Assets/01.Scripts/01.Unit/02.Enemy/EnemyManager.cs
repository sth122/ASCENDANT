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
    [SerializeField] private int _maxSimultaneousAttackTokens = 1; // 일반 전투 룸 기본 1기만 공격 허용
    private int _availableAttackTokens;
    private readonly HashSet<EnemyBTController> _tokenHolders = new HashSet<EnemyBTController>();

    private void Awake()
    {
        ServiceLocator.Register<IEnemyManager>(this, ServiceLifetime.Local);
        _availableAttackTokens = _maxSimultaneousAttackTokens;
        SubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        _registeredEnemies.Clear();
        _defeatedPermanentIds.Clear();
        _tokenHolders.Clear();
    }

    #region 이벤트 구독 (무명 메서드 지양 원칙 준수)
    private void SubscribeEvents()
    {
        UnsubscribeEvents();
        EventBusSystem.Subscribe<BonfireRestedEvent>(HandleBonfireRested);
    }

    private void UnsubscribeEvents()
    {
        EventBusSystem.Unsubscribe<BonfireRestedEvent>(HandleBonfireRested);
    }

    private void HandleBonfireRested(BonfireRestedEvent eventData)
    {
        Util.Log($"[화톳불 상호작용 감지] {eventData.BonfirePosition}");
        ResetAllEnemies();
    }
    #endregion

    #region 공격 시스템
    /// <summary>
    /// 적 AI가 공격을 시작하기 전 공격 권한 티켓 요청
    /// </summary>
    public bool TryAcquireAttackToken(EnemyBTController enemy)
    {
        if(enemy == null) return false;
        if(_tokenHolders.Contains(enemy)) return true;

        if(_availableAttackTokens > 0)
        {
            _availableAttackTokens--;
            _tokenHolders.Add(enemy);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 공격 모션이 종료되거나 피격/취소 시 티켓을 반환ㄴ
    /// </summary>
    public void ReleaseAttackToken(EnemyBTController enemy)
    {
        if (enemy != null && _tokenHolders.Remove(enemy))
        {
            _availableAttackTokens = Mathf.Min(_maxSimultaneousAttackTokens, _availableAttackTokens + 1);
        }
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
            _registeredEnemies.Remove(enemy);
        }
    }

    /// <summary>
    /// 보스나 네임드 적 처치 시 영구 사망 플래그에 등록
    /// </summary>
    public void RegisterDefeatedPermanentEnemy(string permanentId)
    {
        if (string.IsNullOrEmpty(permanentId)) return;

        if (!_defeatedPermanentIds.Contains(permanentId))
        {
            _defeatedPermanentIds.Add(permanentId);
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
        _availableAttackTokens = _maxSimultaneousAttackTokens;

        for (int i = _registeredEnemies.Count - 1; i >= 0; i--)
        {
            EnemyBTController enemy = _registeredEnemies[i];
            if (enemy == null)
            {
                _registeredEnemies.RemoveAt(i);
                continue;
            }

            // 보스나 영구 처치 대상은 비활성화 유지
            if (IsPermanentEnemyDefeated(enemy.name))
            {
                enemy.gameObject.SetActive(false);
                continue;
            }

            // 1. 게임오브젝트 활성화
            enemy.gameObject.SetActive(true);

            // 2. 물리 콜라이더 복원
            if (enemy.Collider != null)
            {
                enemy.Collider.enabled = true;
            }

            // 3. 스폰 지점으로 위치 워프
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

            // 4. 애니메이션 상태를 기본 대기로 강제 리셋
            if (enemy.AnimController != null)
            {
                enemy.AnimController.ResetStateCache();
                enemy.AnimController.PlayState(AnimStateType.Idle, force: true);
            }

            // 5. 스탯 및 컨트롤러 초기화
            enemy.StatusController.HealHp(enemy.StatusController.MaxHp);
            enemy.StatusController.ResetPoiseToDefault();
            enemy.ClearTarget();
            enemy.SetControllerActive(true);
            enemy.Init();
        }
        Util.Log($"총 {_registeredEnemies.Count}기의 적 화톳불 리셋 프로세스 완료");
    }
    #endregion
}