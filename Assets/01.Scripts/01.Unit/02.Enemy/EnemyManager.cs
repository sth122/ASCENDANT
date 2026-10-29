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
        Util.Log($"[화톳불 상호작용 감지] 씬 내 모든 적에 대한 리셋 프로세스를 개시합니다. 위치: {eventData.BonfirePosition}");
        ResetAllEnemies();
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
    /// 화톳불 휴식 시 등록된 모든 에너미에게 리셋을 명령 (Find 연산 배제)
    /// </summary>
    public void ResetAllEnemies()
    {
        for (int i = _registeredEnemies.Count - 1; i >= 0; i--)
        {
            EnemyBTController enemy = _registeredEnemies[i];
            if (enemy == null)
            {
                _registeredEnemies.RemoveAt(i);
                continue;
            }

            // 개별 적의 3단계 초기화 메서드 호출
            //enemy.ResetToOriginState();
        }

        Util.Log($"총 {_registeredEnemies.Count}기의 적 초기화 프로세스 완료");
    }
    #endregion
}