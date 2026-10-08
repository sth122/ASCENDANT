using Cysharp.Threading.Tasks;
using UnityEngine;
using Util = DebugLogger<UnitStatController>;

/// <summary>
/// 모든 Unit(Player, Enemy, NPC)이 공유하는 런타임 가변 수치를 전담 관리하는 추상 컴포넌트
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
    public event System.Action OnPoiseBreakEvent;
    public event System.Action<float> OnHpChanged;

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

        PoiserRecoveryLoopAsync(_cts.Token).Forget();
    }

    public virtual void TakeDamage(float damage, float poiseDamage = 0f)
    {
        _currentHp = Mathf.Max(0f, _currentHp - damage);
        OnHpChanged?.Invoke(_currentHp);

        _poiseRecoveryTimer = PoiseRecoveryDelay;
        _currentPoise = Mathf.Max(0f, _currentPoise - poiseDamage);


        if (_currentHp <= 0f)
        {
            OnDieEvent?.Invoke();
        }

        if (_currentPoise <= 0f)
        {
            OnPoiseBreakEvent?.Invoke();
        }
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
