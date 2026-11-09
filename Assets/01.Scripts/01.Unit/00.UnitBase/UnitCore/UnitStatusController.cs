using Cysharp.Threading.Tasks;
using UnityEngine;
using Util = DebugLogger<UnitStatusController>;

/// <summary>
/// 모든 Unit(Player, Enemy, NPC)이 공유하는 런타임 가변 수치를 전담 관리하는 추상 컴포넌트
/// </summary>
public class UnitStatusController : MonoBehaviour
{
    public UnitMotionStatusModule MotionStatus { get; protected set; }
    #region 런타임 가변 수치 ( 공통 Stat )
    [Header("Unit Shared Stats")]
    [SerializeField] protected float _currentHp;
    [SerializeField] protected float _maxHp;
    [SerializeField] protected float _currentPoise;
    [SerializeField] protected float _maxPoise;
    [SerializeField] protected bool _isInvincible = false;

    protected System.Threading.CancellationTokenSource _poiseCts;

    protected float _poiseRecoveryTimer;
    protected const float PoiseRecoveryDelay = 5.0f;  // 마지막 피격 후 5초 뒤 회복
    protected const float PoiseRecoveryRate = 15.0f;
    protected bool _isPoiseRecoveryPaused = false;
    protected bool _isPoiseDepleted = false;
    protected bool _isDead = false;
    #endregion

    #region Property
    public float CurrentHp => _currentHp;
    public float MaxHp => _maxHp;
    public virtual float CurrentPoise => _currentPoise;
    public virtual float MaxPoise => _maxPoise;
    public virtual bool IsPoiseBroken => _currentPoise <= 0f;
    public virtual bool IsPoiseDepleted => _isPoiseDepleted;
    public bool IsDead => _isDead;
    public bool IsInvincible => _isInvincible;
    #endregion

    public event System.Action<float, float> OnHpChangedEvent;
    public event System.Action<float, float> OnPoiseChangedEvent;
    public event System.Action<bool> OnInvincibleChangedEvent;
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
    public void Init(UnitBaseInfo baseStat)
    {
        // 유닛 공통 폴백. 자가 복구 시도
        if (baseStat == null)
        {
            baseStat = ResolveFallbackStat();
        }

        if (baseStat == null) return;

        // 1. 기존 동작 중인 비동기 루프 안전 정리 (오브젝트 풀링/재초기화 대응)
        AsyncUtil.CancelAndDispose(ref _poiseCts);
        _poiseCts = new System.Threading.CancellationTokenSource();

        // 2. 공통 기초 스탯 값 주입
        _maxHp = baseStat.MaxHp;
        _currentHp = _maxHp;
        _isDead = false;
        _isInvincible = false;

        // 1. 포이즈 초기화 가상 메서드 호출 (Player: Max 시작, Enemy: 0 시작)
        InitPoiseStats(baseStat);

        // 2. 파생 클래스 전용 스탯 및 모듈 확장
        SetupDerivedStats(baseStat);

        // 3. 포이즈 비동기 루프 기동 (Player: Max 회복 루프, Enemy: 0 감쇄 루프)
        StartPoiseLoop();

        InvokeChangeHPEvent(_currentHp, _maxHp);
        InvokeChangePoiseEvent(_currentPoise, _maxPoise);
        SetInvincible(false);
    }

    /// <summary>
    /// MaxPoise로 초기화
    /// </summary>
    protected virtual void InitPoiseStats(UnitBaseInfo baseStat)
    {
        _maxPoise = baseStat.Poise > 0f ? baseStat.Poise : 100f;
        _currentPoise = _maxPoise;
        _poiseRecoveryTimer = 0f;
        _isPoiseRecoveryPaused = false;
        _isPoiseDepleted = false;
    }

    /// <summary>
    /// 세이브 파일 로드 시 피격 경직이나 대미지 연산 없이 현재 체력을 세팅
    /// </summary>
    public void SetHpDirectly(float hp)
    {
        _currentHp = Mathf.Clamp(hp, 0, _maxHp);
        OnHpChangedEvent?.Invoke(_currentHp, _maxHp);
        EventBusSystem.Publish(new UnitHpChangedEvent(gameObject, _currentHp, _maxHp));

        if (_currentHp <= 0f)
        {
            Dead();
        }
    }

    /// <summary>
    /// 자식 클래스에서 오버라이드하여 전용 모션 모듈과 추가 스탯을 세팅하는 가상 메서드
    /// </summary>
    /// <param name="baseStat">자식 타입으로 캐스팅하여 사용할 원본 스탯</param>
    protected virtual void SetupDerivedStats(UnitBaseInfo baseStat) { }

    /// <summary>
    /// 강인도 회복 비동기 루프 기동
    /// </summary>
    protected virtual void StartPoiseLoop()
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
        InvokeChangeHPEvent(_currentHp, _maxHp);

        if (_currentHp <= 0f)
        {
            Dead();
            return;
        }

        ApplyPoiseDamage(poiseDamage);
    }

    /// <summary>
    /// 대미지 피격 시 Max -> 0으로 감산 - 플레이어 기준
    /// </summary>
    protected virtual void ApplyPoiseDamage(float poiseDamage)
    {
        if (!_isPoiseDepleted)
        {
            _poiseRecoveryTimer = PoiseRecoveryDelay;
            _currentPoise = Mathf.Max(0f, _currentPoise - poiseDamage);
            InvokeChangePoiseEvent(_currentPoise, _maxPoise);

            if (_currentPoise <= 0f)
            {
                PoiseBreak();
            }
        }
    }

    public virtual void SetInvincible(bool isInvincible)
    {
        if (_isInvincible == isInvincible) return;
        _isInvincible = isInvincible;
        InvokeChangeInvincibleEvent(isInvincible);
    }

    /// <summary>
    /// 체력 회복 메서드
    /// </summary>
    public virtual void HealHp(float healAmount)
    {
        if (_isDead || healAmount <= 0f) return;

        _currentHp = Mathf.Min(_maxHp, _currentHp + healAmount);
        InvokeChangeHPEvent(_currentHp, _maxHp);
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
    }

    /// <summary>
    /// 그로기 발생 메서드
    /// </summary>
    protected virtual void PoiseBreak()
    {
        _isPoiseDepleted = true;
        OnPoiseBreakEvent?.Invoke();
    }

    public void SetPoiseRecoveryPause(bool isPause) => _isPoiseRecoveryPaused = isPause;

    /// <summary>
    /// Poise 초기 정상로 리셋
    /// </summary>
    public virtual void ResetPoiseToDefault()
    {
        _currentPoise = _maxPoise;
        _poiseRecoveryTimer = 0f;
        _isPoiseRecoveryPaused = false;
        _isPoiseDepleted = false;

        InvokeChangePoiseEvent(_currentPoise, _maxPoise);
    }

    /// <summary>
    /// 강인도 자동 회복 비동기 루프
    /// 피격 딜레이(4초)가 지난 후 초당 20씩 최대치까지 회복
    /// </summary>
    private async UniTaskVoid PoiseRecoveryLoopAsync(System.Threading.CancellationToken token)
    {
        float lastPublishedPoise = _currentPoise;

        while (!token.IsCancellationRequested)
        {
            bool isCanceled = await UniTask.Delay(100, cancellationToken: token).SuppressCancellationThrow();
            if (isCanceled) return;

            // 강인도 회복 정지 상태 or 사망 or 최대치라면 보류
            if (_isPoiseRecoveryPaused || _isDead ||_currentPoise >= _maxPoise) continue;

            if (_poiseRecoveryTimer > 0f)
            {
                _poiseRecoveryTimer -= 0.1f;
            }
            else
            {
                _currentPoise = Mathf.Min(_maxPoise, _currentPoise + PoiseRecoveryRate * 0.1f);

                if (Mathf.Abs(_currentPoise - lastPublishedPoise) >= 1.0f || _currentPoise >= _maxPoise)
                {
                    lastPublishedPoise = _currentPoise;
                    InvokeChangePoiseEvent(_currentPoise, _maxPoise);
                }

                if (_currentPoise >= _maxPoise)
                {
                    _isPoiseDepleted = false;
                }
            }
        }
    }

    #region 유닛 데이터 폴백
    /// <summary>
    /// [모든 유닛 공통 데이터 폴백 리졸버]
    /// UnitController의 UnitIdentifier를 기반으로 DataManager에서 적합한 스탯을 자동 복구합니다.
    /// </summary>
    protected virtual UnitBaseInfo ResolveFallbackStat()
    {
        if (!TryGetComponent<UnitController>(out var unitController))
        {
            Util.LogError($"{gameObject.name}: UnitController 컴포넌트를 찾을 수 없어 Fallback 스탯 조회가 불가능합니다.");
            return null;
        }

        UnitId unitId = unitController.UnitIdentifier;
        if (DataManager.Instance == null)
        {
            Util.LogError($"{gameObject.name}: DataManager 인스턴스가 존재하지 않습니다.");
            return null;
        }

        // 1. 플레이어 데이터베이스(PlayerStatusSO) 조회
        if (DataManager.Instance.TryGetSingle<PlayerStatusSO>(out var playerStatusSO) && playerStatusSO != null)
        {
            if (playerStatusSO.TryGetData(unitId, out PlayerInfo playerInfo) && playerInfo != null)
            {
                Util.Log($"{gameObject.name}: DataManager(PlayerStatusSO)로부터 [{unitId}] 스탯을 성공적으로 자가 복구했습니다.");
                return playerInfo.Clone();
            }
        }

        // 2. 보스 데이터베이스(BossInfoSO) 조회
        if (DataManager.Instance.TryGetSingle<BossInfoSO>(out var bossInfoSO) && bossInfoSO != null)
        {
            if (bossInfoSO.TryGetData(unitId, out BossInfo bossInfo) && bossInfo != null)
            {
                Util.Log($"{gameObject.name}: DataManager(BossInfoSO)로부터 [{unitId}] 스탯을 성공적으로 자가 복구했습니다.");
                return bossInfo.Clone();
            }
        }

        // 3. 일반 에너미 데이터베이스(EnemyStatusSO) 조회
        if (DataManager.Instance.TryGetSingle<EnemyStatusSO>(out var enemyStatusSO) && enemyStatusSO != null)
        {
            if (enemyStatusSO.TryGetData(unitId, out EnemyInfo enemyInfo) && enemyInfo != null)
            {
                Util.Log($"{gameObject.name}: DataManager(EnemyStatusSO)로부터 [{unitId}] 스탯을 성공적으로 자가 복구했습니다.");
                return enemyInfo.Clone();
            }
        }

        Util.LogError($"{gameObject.name}: DataManager의 모든 SO에서 [{unitId}] 스탯을 찾을 수 없습니다.");
        return null;
    }

    /// <summary>
    /// PlayerStatusController 등 파생 클래스에서 고유 SO를 조회할 수 있도록 개방하는 가상 메서드
    /// </summary>
    protected virtual UnitBaseInfo ResolveCustomFallbackStat(UnitId unitId) => null;
    #endregion

    #region Event 래퍼 메서드
    /// <summary>
    /// 강인도 변경 이벤트 및 전역 EventBus 발행 래퍼 메서드 (자식 클래스 Invoke 허용)
    /// </summary>
    protected void InvokeChangePoiseEvent(float currentPoise, float maxPoise)
    {
        OnPoiseChangedEvent?.Invoke(currentPoise, maxPoise);
        EventBusSystem.Publish(new UnitPoiseChangedEvent(gameObject, currentPoise, maxPoise));
    }

    /// <summary>
    /// 체력 변경 이벤트 및 전역 EventBus 발행 래퍼 메서드 (자식 클래스 Invoke 허용)
    /// </summary>
    protected void InvokeChangeHPEvent(float currentHp, float maxHp)
    {
        OnHpChangedEvent?.Invoke(currentHp, maxHp);
        EventBusSystem.Publish(new UnitHpChangedEvent(gameObject, currentHp, maxHp));
    }

    protected void InvokeChangeInvincibleEvent(bool isInvincible)
    {
        OnInvincibleChangedEvent?.Invoke(isInvincible);
        EventBusSystem.Publish(new UnitInvincibleChangedEvent(gameObject, isInvincible));
    }
    #endregion

    protected virtual void OnDestroy() => AsyncUtil.CancelAndDispose(ref _poiseCts);
}
