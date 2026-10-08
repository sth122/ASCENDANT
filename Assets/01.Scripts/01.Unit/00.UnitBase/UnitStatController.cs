using Cysharp.Threading.Tasks;
using UnityEngine;
using Util = DebugLogger<UnitStatController>;

/// <summary>
/// 모든 Unit(Player, Enemy, NPC)이 공유하는 공통 스탯 모듈을 관리하는 Base 컴포넌트
/// 공통 스탯만을 인스펙터에 노출하며, 세부 Unit 전용 스탯은 하위 클래스 컴포넌트에서 동작
/// </summary>
public class UnitStatController : MonoBehaviour
{
    public UnitMotionStatModule MotionStats { get; protected set; }
    #region 런타임 가변 수치 ( 공통 Stat )
    [Header("Unit Shared")]
    [SerializeField] protected float _currentHp;
    [SerializeField] protected float _maxHp;
    [SerializeField] protected float _currentPoise;
    [SerializeField] protected float _maxPoise;
    [SerializeField] protected int _dropSouls;

    protected System.Threading.CancellationTokenSource _cts;

    protected float _poiseRecoveryTimer;
    protected const float PoiseRecoveryDelay = 4.0f;  // 마지막 피격 후 4초 뒤 회복
    protected const float PoiseRecoveryRate = 20.0f;

    public float CurrentHp => _currentHp;
    public float MaxHp => _maxHp;
    public float CurrentPoise => _currentPoise;
    public int DropSouls => _dropSouls;
    public bool IsPoiseBroken => _currentPoise <= 0f;
    #endregion

    public event System.Action OnDieEvent;
    public event System.Action<float> OnHpChanged;
    public event System.Action OnPoiseBreakEvent;

    protected virtual void Awake()
    {
        _cts = new System.Threading.CancellationTokenSource();
    }

    public virtual void Init(UnitBaseStat baseStat)
    {
        if (baseStat == null)
        {
            Util.LogError($"{gameObject.name} 전달된 UnitBaseStat이 Null");
            return;
        }

        _maxHp = baseStat.MaxHp;
        _currentHp = MaxHp;
        _currentPoise = baseStat.Poise;
        _dropSouls = baseStat.DropSouls;
        MotionStats = new UnitMotionStatModule(baseStat);
    }


    private async UniTaskVoid PoiserRecoveryLoopAsync(System.Threading.CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);

            if (_currentPoise >= 0f) continue;

            if (_poiseRecoveryTimer > 0f)
            {
                _poiseRecoveryTimer -= Time.deltaTime;
            }
            else
            {
                _currentPoise = Mathf.Min(_maxPoise, _currentPoise + PoiseRecoveryRate * Time.deltaTime);
            }
        }
    }

    protected virtual void OnDestory()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}
