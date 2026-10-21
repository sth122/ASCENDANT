using UnityEngine;
using Util = DebugLogger<EnemyStatusController>;

/// <summary>
/// Enemy 전용 확장 런타임 스탯 컨트롤러
/// </summary>
public class EnemyStatusController : UnitStatController
{
    public EnemyMotionStatusModule EnemyMotionStatus => MotionStatus as EnemyMotionStatusModule;
    public EnemyStatus EnemyStatusSource { get; private set; }

    [Header("Enemy ExclusiveL Souls")]
    [SerializeField] private int _dropSouls;
    public int DropSouls => _dropSouls;

    /// <summary>
    /// Enemy 전용 스탯 및 모듈 확장부
    /// </summary>
    /// <param name="baseStat"></param>
    protected override void SetupDerivedStats(UnitBaseStatus baseStat)
    {
        if (baseStat is EnemyStatus enemyStat)
        {
            EnemyStatusSource = enemyStat;

            _maxHp = enemyStat.MaxHp;
            _currentHp = _maxHp;
            _currentPoise = enemyStat.Poise;
            _maxPoise = enemyStat.Poise;
            _dropSouls = enemyStat.DropSouls;

            MotionStatus = new EnemyMotionStatusModule(enemyStat);
        }
        else
        {
            Util.LogWarning($"{gameObject.name}: 전달된 스탯이 EnemyStat 타입과 일치하지 않음");
        }
    }
}
