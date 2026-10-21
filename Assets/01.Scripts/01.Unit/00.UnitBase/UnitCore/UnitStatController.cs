using Cysharp.Threading.Tasks;
using UnityEngine;
using Util = DebugLogger<UnitStatController>;

/// <summary>
/// 모든 Unit(Player, Enemy, NPC)이 공유하는 런타임 가변 수치를 전담 관리하는 추상 컴포넌트
/// </summary>
public class UnitStatController : MonoBehaviour
{
    public UnitMotionStatusModule MotionStatus { get; protected set; }
    #region 런타임 가변 수치 ( 공통 Stat )
    [Header("Unit Shared Stats")]
    [SerializeField] protected float _currentHp;
    [SerializeField] protected float _maxHp;
    [SerializeField] protected float _currentPoise;
    [SerializeField] protected float _maxPoise;

    protected System.Threading.CancellationTokenSource _poiseCts;

    protected float _poiseRecoveryTimer;
    protected const float PoiseRecoveryDelay = 5.0f;  // 마지막 피격 후 5초 뒤 회복
    protected const float PoiseRecoveryRate = 15.0f;
    protected bool _isPoiseRecoveryPaused = false;
    protected bool _isPoiseDepleted = false;
    protected bool _isDead = false;

    #region Property
    public float CurrentHp => _currentHp;
    public float MaxHp => _maxHp;
    public float CurrentPoise => _currentPoise;
    public float MaxPoise => _maxPoise;
    public bool IsPoiseBroken => _currentPoise <= 0f;
    public bool IsPoiseDepleted => _isPoiseDepleted;
    public bool IsDead => _isDead;
    #endregion
    #endregion

    public event System.Action<float, float> OnHpChangedEvent;
    public event System.Action<float, float> OnPoiseChangedEvent;
    public event System.Action OnDieEvent;
    public event System.Action OnPoiseBreakEvent;

    protected virtual void Awake()
    {
        _poiseCts = new System.Threading.CancellationTokenSource();
    }

    /// <summary>
    /// 스탯 초기화 템플릿 메서드
    /// 공통 스탯 세팅 -> 자식 전용 스탯 및 모듈 확장 -> 루프 가동 순서
    /// </summary>
    /// <param name="baseStat">주입할 기본 스탯 데이터</param>
    public void Init(UnitBaseStatus baseStat)
    {
        if (baseStat == null)
        {
            Util.LogError($"{gameObject.name}: 전달된 UnitBaseStat이 Null");
            return;
        }

        // 1. 기존 동작 중인 비동기 루프 안전 정리 (오브젝트 풀링/재초기화 대응)
        AsyncUtil.CancelAndDispose(ref _poiseCts);
        _poiseCts = new System.Threading.CancellationTokenSource();

        // 2. 공통 기초 스탯 값 주입
        _maxHp = baseStat.MaxHp;
        _currentHp = _maxHp;
        _maxPoise = baseStat.Poise;
        _currentPoise = _maxPoise;
        _poiseRecoveryTimer = 0f;
        _isPoiseRecoveryPaused = false;
        _isPoiseDepleted = false;
        _isDead = false;

        // 3. 자식 클래스 전용 스탯 및 모듈 세팅 위임
        SetupDerivedStats(baseStat);

        // 4. 모든 스탯과 모듈이 완전히 구성된 루프 가동
        StartPoiseRecoveryLoop();

        OnHpChangedEvent?.Invoke(_currentHp, _maxHp);
        OnPoiseChangedEvent?.Invoke(_currentPoise, _maxPoise);

        EventBusSystem.Publish(new UnitHpChangedEvent(gameObject, _currentHp, _maxHp));
        EventBusSystem.Publish(new UnitPoiseChangedEvent(gameObject, _currentPoise, _maxPoise));
    }

    /// <summary>
    /// 자식 클래스에서 오버라이드하여 전용 모션 모듈과 추가 스탯을 세팅하는 가상 메서드
    /// </summary>
    /// <param name="baseStat">자식 타입으로 캐스팅하여 사용할 원본 스탯</param>
    protected virtual void SetupDerivedStats(UnitBaseStatus baseStat) { }

    /// <summary>
    /// 강인도 회복 비동기 루프 기동
    /// </summary>
    protected void StartPoiseRecoveryLoop()
    {
        if (_poiseCts != null && _maxPoise > 0f)
        {
            PoiseRecoveryLoopAsync(_poiseCts.Token).Forget();
        }
    }

    public virtual void TakeDamage(float damage, float poiseDamage = 0f)
    {
        if (_isDead) return;

        _currentHp = Mathf.Max(0f, _currentHp - damage);
        OnHpChangedEvent?.Invoke(_currentHp, _maxHp);
        EventBusSystem.Publish(new UnitHpChangedEvent(gameObject, _currentHp, _maxHp));

        _poiseRecoveryTimer = PoiseRecoveryDelay;
        _currentPoise = Mathf.Max(0f, _currentPoise - poiseDamage);
        OnPoiseChangedEvent?.Invoke(_currentPoise, _maxPoise);
        EventBusSystem.Publish(new UnitPoiseChangedEvent(gameObject, _currentPoise, _maxPoise));


        if (_currentHp <= 0f)
        {
            Dead();
            return;
        }

        if (_currentPoise <= 0f)
        {
            PoiseBreak();
        }
    }

    /// <summary>
    /// 체력 회복 메서드
    /// </summary>
    public virtual void HealHp(float healAmount)
    {
        if (_isDead || healAmount <= 0f) return;

        _currentHp = Mathf.Min(_maxHp, _currentHp + healAmount);
        OnHpChangedEvent?.Invoke(_currentHp, _maxHp);
        EventBusSystem.Publish(new UnitHpChangedEvent(gameObject, _currentHp, _maxHp));
    }

    /// <summary>
    /// 사망 처리 메서드
    /// </summary>
    protected virtual void Dead()
    {
        if (_isDead) return;
        _isDead = true;

        AsyncUtil.CancelAndDispose(ref _poiseCts);

        OnDieEvent?.Invoke();

        UnitId identifier = GetComponent<PlayerController>() != null ? UnitId.Player : UnitId.NightmareCreature;
        EventBusSystem.Publish(new UnitDiedEvent(gameObject, identifier));

        Util.Log($"{gameObject.name} 사망");
    }

    /// <summary>
    /// 그로기 발생 메서드
    /// </summary>
    protected virtual void PoiseBreak()
    {
        _isPoiseDepleted = true;
        OnPoiseBreakEvent?.Invoke();
        Util.Log($"{gameObject.name} 그로기 발생");
    }

    public void SetPoiseRecoveryPause(bool isPause) => _isPoiseRecoveryPaused = isPause;

    /// <summary>
    /// Poise 최대치로 즉시 복구
    /// </summary>
    public void RestorePoiseToMax()
    {
        _currentPoise = _maxPoise;
        _poiseRecoveryTimer = 0f;
        _isPoiseRecoveryPaused = false;
        _isPoiseDepleted = false;

        OnPoiseChangedEvent?.Invoke(_currentPoise, _maxPoise);
        EventBusSystem.Publish(new UnitPoiseChangedEvent(gameObject, _currentPoise, _maxPoise));
    }

    /// <summary>
    /// 강인도 자동 회복 비동기 루프
    /// 피격 딜레이(4초)가 지난 후 초당 20씩 최대치까지 회복
    /// </summary>
    private async UniTaskVoid PoiseRecoveryLoopAsync(System.Threading.CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            bool isCanceled =  await UniTask.Yield(token).SuppressCancellationThrow();
            if(isCanceled) return;

            // 강인도 회복 정지 상태 or 사망 or 최대치라면 보류
            if (_isPoiseRecoveryPaused || _isDead ||_currentPoise >= _maxPoise) continue;

            if (_poiseRecoveryTimer > 0f)
            {
                _poiseRecoveryTimer -= Time.deltaTime;
            }
            else
            {
                _currentPoise = Mathf.Min(_maxPoise, _currentPoise + PoiseRecoveryRate * Time.deltaTime);
                OnPoiseChangedEvent?.Invoke(_currentPoise, _maxPoise);
                EventBusSystem.Publish(new UnitPoiseChangedEvent(gameObject, _currentPoise, _maxPoise));
                if (_currentPoise >= _maxPoise)
                {
                    _isPoiseDepleted = false;
                }
            }
        }
    }

    protected virtual void OnDestroy() => AsyncUtil.CancelAndDispose(ref _poiseCts);

    protected void InvokeChangeHPEvent(float currentHp, float maxHp)
    {
        OnHpChangedEvent?.Invoke(currentHp, maxHp);
    }
}
