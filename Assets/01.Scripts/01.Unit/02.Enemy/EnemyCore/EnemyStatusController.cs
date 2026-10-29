using UnityEngine;
using Util = DebugLogger<EnemyStatusController>;

/// <summary>
/// Enemy 전용 확장 런타임 스탯 컨트롤러
/// </summary>
public class EnemyStatusController : UnitStatusController
{
    [SerializeReference] private EnemyInfo _runtimeEnemyInfo;

    public EnemyMotionStatusModule EnemyMotionStatus => MotionStatus as EnemyMotionStatusModule;
    public EnemyInfo EnemyInfoSource => _runtimeEnemyInfo;

    [Header("Enemy ExclusiveL Souls")]
    [SerializeField] private int _dropSouls;
    public int DropSouls => _dropSouls;

    public event System.Action<EnemyInfo> OnStatusDataChanged;

    /// <summary>
    /// Enemy 전용 스탯 및 모듈 확장부
    /// </summary>
    /// <param name="baseStat"></param>
    protected override void SetupDerivedStats(UnitBaseInfo baseStat)
    {
        if (baseStat is EnemyInfo enemyStat)
        {
            _runtimeEnemyInfo = (EnemyInfo)enemyStat.Clone();

            MotionStatus = new EnemyMotionStatusModule(enemyStat);

            SyncStatsFromRuntimeInfo(isInitialSetup: true);
        }
        else
        {
            Util.LogWarning($"{gameObject.name}: 전달된 스탯이 EnemyStat 타입과 일치하지 않음");
        }
    }

    /// <summary>
    /// 인스펙터의 _runtimeEnemyInfo 수치를 컨트롤러 캐시 및 모듈에 동기화
    /// </summary>
    public void SyncStatsFromRuntimeInfo(bool isInitialSetup = false)
    {
        if (_runtimeEnemyInfo == null) return;

        float prevMaxHp = _maxHp;
        _maxHp = _runtimeEnemyInfo.MaxHp;

        if (isInitialSetup)
        {
            _currentHp = _maxHp;
            _maxPoise = _runtimeEnemyInfo.Poise;
            _currentPoise = _maxPoise;
        }
        else
        {
            // 런타임 튜닝 중 최대 체력이 변경된 경우 현재 체력 비율 유지 보정
            if (prevMaxHp > 0f)
            {
                float hpRatio = _currentHp / prevMaxHp;
                _currentHp = Mathf.Clamp(_maxHp * hpRatio, 1f, _maxHp);
            }
            _maxPoise = _runtimeEnemyInfo.Poise;
            _currentPoise = Mathf.Min(_currentPoise, _maxPoise);
        }

        _dropSouls = _runtimeEnemyInfo.DropSouls;

        // 이동 모듈 스탯 동기화
        MotionStatus = new EnemyMotionStatusModule(_runtimeEnemyInfo);

        // 상위 컨트롤러(EnemyBTController)의 지각 센서 및 기즈모 동기화 신호 발행
        OnStatusDataChanged?.Invoke(_runtimeEnemyInfo);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 플레이 모드 중 인스펙터에서 _runtimeEnemyInfo의 수치를 직접 편집했을 때 즉시 반영
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying && _runtimeEnemyInfo != null)
        {
            SyncStatsFromRuntimeInfo(isInitialSetup: false);
        }
    }
#endif
}
