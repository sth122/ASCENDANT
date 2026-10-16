using Util = DebugLogger<EnemyStatController>;

/// <summary>
/// Enemy 전용 확장 런타임 스탯 컨트롤러
/// </summary>
public class EnemyStatController : UnitStatController
{
    public EnemyStat EnemyStatSource { get; private set; }

    public override void Init(UnitBaseStat baseStat)
    {
        base.Init(baseStat);

        if (baseStat is EnemyStat enemyStat)
        {
            EnemyStatSource = enemyStat;
            _maxHp = enemyStat.MaxHp;
            _currentHp = _maxHp;
            _currentPoise = enemyStat.Poise;
            _maxPoise = enemyStat.Poise;
            _dropSouls = enemyStat.DropSouls;
        }
        else
        {
            Util.LogWarning($"{gameObject.name}: 전달된 스탯이 EnemyStat 타입과 일치하지 않음");
        }
    }
}
