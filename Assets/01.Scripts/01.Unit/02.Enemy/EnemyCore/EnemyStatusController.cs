using Cysharp.Threading.Tasks;
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

    [Header("Groggy Gauge Settings")]
    [SerializeField] private float _poiseDecayDelay = 4.0f; // 미피격 시 4초 뒤부터 감쇄
    [SerializeField] private float _poiseDecayRate = 12.0f;  // 초당 12씩 0으로 자연 감소
    private float _poiseDecayTimer = 0f;

    #region 2페이즈 플래그
    private bool _isPhase2Pending = false;
    private bool _hasTriggeredPhase2 = false;
    public bool IsPhase2Pending => _isPhase2Pending;
    public bool HasTriggeredPhase2 => _hasTriggeredPhase2;

    #endregion

    public event System.Action<EnemyInfo> OnStatusDataChanged;

    public int DropSouls => _dropSouls;
    public override bool IsPoiseBroken => _currentPoise >= _maxPoise;   // 100%이상 누적되었을 때 그로기 판정

    protected override void InitPoiseStats(UnitBaseInfo baseStat)
    {
        _maxPoise = baseStat.Poise > 0f ? baseStat.Poise : 100f;
        _currentPoise = 0f;
        _poiseDecayTimer = 0f;
        _isPoiseRecoveryPaused = false;
        _isPoiseDepleted = false;
        _isPhase2Pending = false;
        _hasTriggeredPhase2 = false;
    }

    /// <summary>
    /// Enemy 전용 스탯 및 모듈 확장부
    /// </summary>
    /// <param name="baseStat"></param>
    protected override void SetupDerivedStats(UnitBaseInfo baseStat)
    {
        if (!(baseStat is EnemyInfo enemyStat))
        {
            enemyStat = ResolveFallbackStat() as EnemyInfo;
        }

        if (enemyStat != null)
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

        _maxHp = _runtimeEnemyInfo.MaxHp;

        if (isInitialSetup)
        {
            _currentHp = _maxHp;
            _maxPoise = _runtimeEnemyInfo.Poise > 0f ? _runtimeEnemyInfo.Poise : 100f;
            _currentPoise = 0f;
        }

        _dropSouls = _runtimeEnemyInfo.DropSouls;
        OnStatusDataChanged?.Invoke(_runtimeEnemyInfo);
    }

    /// <summary>
    /// 미피격 시 0을 향해 자연 감쇄하는 루프 가동
    /// </summary>
    protected override void StartPoiseLoop()
    {
        if (_poiseCts != null)
        {
            PoiseNaturalDecayLoopAsync(_poiseCts.Token).Forget();
        }
    }

    public override void TakeDamage(float damage, float poiseDamage = 0f)
    {
        if (_isDead || _isInvincible) return;

        // 보스가 그로기 중일 때 페이즈 2 기준선 도달 검사
        if (_runtimeEnemyInfo is BossInfo bossInfo && _isPoiseDepleted && !_hasTriggeredPhase2 && !_isPhase2Pending)
        {
            float thresholdHp = _maxHp * bossInfo.Phase2ThresholdRatio;
            if (_currentHp - damage <= thresholdHp)
            {
                _currentHp = thresholdHp;
                InvokeChangeHPEvent(_currentHp, _maxHp);
                // 그로기가 끝날 때까지 즉시 무적화(체력바 은색 전환) 후 대기 플래그 설정
                SetInvincible(true);
                _isPhase2Pending = true;
                return;
            }
        }

        base.TakeDamage(damage, poiseDamage);
    }

    /// <summary>
    /// 대미지를 입으면 0 -> MaxPoise로 누적. 그로기 중에는 추가 누적 불가
    /// </summary>
    protected override void ApplyPoiseDamage(float poiseDamage)
    {
        if (!_isPoiseDepleted && !_isInvincible)
        {
            _poiseDecayTimer = _poiseDecayDelay;
            _currentPoise = Mathf.Min(_maxPoise, _currentPoise + poiseDamage);
            InvokeChangePoiseEvent(_currentPoise, _maxPoise);

            if (_currentPoise >= _maxPoise)
            {
                PoiseBreak();
            }
        }
    }

    /// <summary>
    /// 그로기 지속시간 동안 EnemyGroggyModule에서 호출하여 Max -> 0으로 카운트다운
    /// </summary>
    public void SetGroggyCountdownPoise(float remainingRatio)
    {
        if (!_isPoiseDepleted) return;

        _currentPoise = Mathf.Clamp(remainingRatio * _maxPoise, 0f, _maxPoise);
        InvokeChangePoiseEvent(_currentPoise, _maxPoise);
    }

    public override void ResetPoiseToDefault()
    {
        _currentPoise = 0f;
        _poiseDecayTimer = 0f;
        _isPoiseDepleted = false;
        InvokeChangePoiseEvent(_currentPoise, _maxPoise);
    }

    /// <summary>
    /// 2페이즈 진입 시 호출: 대기 플래그를 소비하고 2페이즈 진입 완료를 기록 (무적 해제는 2페이즈 연출 종료 시 수행)
    /// </summary>
    public void MarkPhase2Triggered()
    {
        _isPhase2Pending = false;
        _hasTriggeredPhase2 = true;
    }

    public void ClearPhase2Pending()
    {
        _isPhase2Pending = false;
        _hasTriggeredPhase2 = true;
        SetInvincible(false);
    }

    /// <summary>
    /// 비전투 지속 시 누적된 그로기 수치가 서서히 0으로 빠지는 루프
    /// </summary>
    private async UniTaskVoid PoiseNaturalDecayLoopAsync(System.Threading.CancellationToken token)
    {
        float lastPublishedPoise = _currentPoise;

        while (!token.IsCancellationRequested)
        {
            bool isCanceled = await UniTask.Delay(100, cancellationToken: token).SuppressCancellationThrow();
            if (isCanceled) return;

            if (_isPoiseDepleted || _isPoiseRecoveryPaused || _isDead || _currentPoise <= 0f) continue;

            if (_poiseDecayTimer > 0f)
            {
                _poiseDecayTimer -= 0.1f;
            }
            else
            {
                _currentPoise = Mathf.Max(0f, _currentPoise - _poiseDecayRate * 0.1f);

                if (Mathf.Abs(_currentPoise - lastPublishedPoise) >= 1.0f || _currentPoise <= 0f)
                {
                    lastPublishedPoise = _currentPoise;
                    InvokeChangePoiseEvent(_currentPoise, _maxPoise);
                }
            }
        }
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
