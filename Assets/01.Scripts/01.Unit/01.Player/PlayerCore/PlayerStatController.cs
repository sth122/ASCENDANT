using Cysharp.Threading.Tasks;
using Util = DebugLogger<PlayerStatController>;

/// <summary>
/// Player 전용 확장 스탯 컴포넌트
/// Player 전용 스태미나 소모 및 회복 루프, 가드 상태에 따른 스태미나 감쇄 및 가드 브레이크 판정을 전담하는 컴포넌트
/// </summary>
public class PlayerStatController : UnitStatController
{
    #region Player 전용 스태미나 수치
    [UnityEngine.Header("Player Exclusive: Stamina")]
    [UnityEngine.SerializeField] private float _currentStamina;
    [UnityEngine.SerializeField] private float _maxStamina;
    [UnityEngine.SerializeField] private float _staminaRegenRate = 10.0f;
    private const float StaminaRegenDelay = 1.0f; // 스태미나 소모 후 회복 시작까지의 지연 시간
    private float _staminaRecoveryTimer = 0f;

    // 스태미나 자연 회복 속도 배율
    public float StaminaRegenMultiplier { get; set; } = 1.0f;
    public float CurrentStamina => _currentStamina;
    public float MaxStamina => _maxStamina;
    #endregion

    public PlayerMotionStatModule PlayerMotionStats => MotionStats as PlayerMotionStatModule;
    public PlayerStat PlayerStatSource { get; private set; }

    /// <summary>
    /// Player 전용 스탯 및 모듈 확장부
    /// </summary>
    protected override void SetupDerivedStats(UnitBaseStat baseStat)
    {
        if (baseStat is PlayerStat playerStat)
        {
            PlayerStatSource = playerStat;

            // Player 전용 공식에 의한 Max 수치 계산
            _maxHp = playerStat.CalculateMaxHp();
            _currentHp = _maxHp;
            _maxStamina = playerStat.CalculateMaxStamina();
            _currentStamina = _maxStamina;

            // Player 전용 모션 모듈 생성 (장비 무게 로직 탑재)
            MotionStats = new PlayerMotionStatModule(playerStat);

            // 스태미나 회복 비동기 루프 가동
            StartStaminaRegenLoop();
        }
        else
        {
            Util.LogError($"{gameObject.name}: 주입된 스탯 데이터가 PlayerStat 타입과 일치하지 않음");
        }
    }
    private void StartStaminaRegenLoop()
    {
        if (_cts != null)
        {
            StaminaRegenLoopAsync(_cts.Token).Forget();
        }
    }

    /// <summary>
    /// 인스펙터에서 수정된 PlayerStat 수치를 런타임 모듈에 실시간 주입
    /// </summary>
    public void SyncRuntimeStats(PlayerStat updatedStat)
    {
        if (updatedStat == null || PlayerMotionStats == null) return;

        PlayerStatSource = updatedStat;
        _maxHp = updatedStat.CalculateMaxHp();
        _maxStamina = updatedStat.CalculateMaxStamina();

        PlayerMotionStats.SyncBaseValues(updatedStat);
    }

    public bool HasEnoughStamina(float cost) => _currentStamina >= cost;

    public void ConsumeStamina(float cost)
    {
        _currentStamina = UnityEngine.Mathf.Max(0f, _currentStamina - cost);
        _staminaRecoveryTimer = StaminaRegenDelay;
    }

    /// <summary>
    /// 가드 시 데미지 블록 처리
    /// </summary>
    /// <param name="incomingDamage">적 공격 데미지</param>
    /// <param name="shieldStability">방패 버티기(감쇄력) 수치</param>
    /// <returns>스태미나가 전부 소모하여 가드 브레이크 발생 시 true</returns>
    public bool BlockAttackWithStamina(float incomingDamage, float shieldStability)
    {
        float staminaCost = incomingDamage * (1.0f - UnityEngine.Mathf.Clamp01(shieldStability));
        _currentStamina -= staminaCost;

        // 가드 피격 시에도 스태미나 회복 지연 타이머 갱신 ( 무한 가드 방어 )
        _staminaRecoveryTimer = StaminaRegenDelay;

        if (_currentStamina <= 0f)
        {
            _currentStamina = 0f;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 스태미나 자동 회복 비동기 루프
    /// </summary>
    private async UniTaskVoid StaminaRegenLoopAsync(System.Threading.CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (isCanceled) return;

            if (_staminaRecoveryTimer > 0f)
            {
                _staminaRecoveryTimer -= UnityEngine.Time.deltaTime;
                continue;
            }

            if (_currentStamina < _maxStamina)
            {
                float finalRate = _staminaRegenRate * StaminaRegenMultiplier;
                _currentStamina = UnityEngine.Mathf.Min(_maxStamina, _currentStamina + finalRate * UnityEngine.Time.deltaTime);
            }
        }
    }
}
