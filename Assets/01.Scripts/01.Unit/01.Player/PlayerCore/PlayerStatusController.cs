using Cysharp.Threading.Tasks;
using Util = DebugLogger<PlayerStatusController>;

/// <summary>
/// Player 전용 확장 스탯 컴포넌트
/// Player 전용 스태미나 소모 및 회복 루프, 가드 상태에 따른 스태미나 감쇄 및 가드 브레이크 판정을 전담하는 컴포넌트
/// </summary>
public class PlayerStatusController : UnitStatusController
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

    #region Player 장비 연동 스탯
    [UnityEngine.Header("Equipped Armor Defense & Shield Spec")]
    [UnityEngine.SerializeField] private float _equippedArmorFlatDefense = 0f;
    [UnityEngine.SerializeField] private float _equippedArmorAbsorption = 0f;
    [UnityEngine.SerializeField] private float _equippedArmorPoiseBonus = 0f;
    [UnityEngine.SerializeField] private float _equippedShieldStability = 0f;
    [UnityEngine.SerializeField] private float _equippedShieldAbsorption = 0f;

    [UnityEngine.SerializeField] private int _equippedBonusStrength = 0;
    [UnityEngine.SerializeField] private int _equippedBonusDexterity = 0;
    [UnityEngine.SerializeField] private float _equippedBonusMaxHp = 0f;
    [UnityEngine.SerializeField] private float _equippedBonusMaxStamina = 0f;
    [UnityEngine.SerializeField] private float _equippedBonusAttackPower = 0f;

    public float EquippedArmorFlatDefense => _equippedArmorFlatDefense;
    public float EquippedArmorAbsorption => _equippedArmorAbsorption;
    public float EquippedArmorPoiseBonus => _equippedArmorPoiseBonus;
    public float EquippedShieldStability => _equippedShieldStability;
    public float EquippedShieldAbsorption => _equippedShieldAbsorption;
    public int EquippedBonusStrength => _equippedBonusStrength;
    public int EquippedBonusDexterity => _equippedBonusDexterity;
    public float EquippedBonusAttackPower => _equippedBonusAttackPower;
    #endregion

    public PlayerMotionStatusModule PlayerMotionStats => MotionStatus as PlayerMotionStatusModule;
    public PlayerInfo PlayerInfoSource { get; private set; }
    public event System.Action<float, float> OnStaminaChangedEvent;

    /// <summary>
    /// Player 전용 스탯 및 모듈 확장부
    /// </summary>
    protected override void SetupDerivedStats(UnitBaseInfo baseStat)
    {
        if (!(baseStat is PlayerInfo playerStat))
        {
            playerStat = ResolveFallbackStat() as PlayerInfo;
        }

        if (playerStat != null)
        {
            PlayerInfoSource = (PlayerInfo)playerStat.Clone();

            // Player 전용 공식에 의한 Max 수치 계산
            _maxHp = playerStat.CalculateMaxHp();
            _currentHp = _maxHp;
            _maxStamina = playerStat.CalculateMaxStamina();
            _currentStamina = _maxStamina;

            // Player 전용 모션 모듈 생성 (장비 무게 로직 탑재)
            MotionStatus = new PlayerMotionStatusModule(playerStat);

            // 스태미나 회복 비동기 루프 가동
            StartStaminaRegenLoop();

            InvokeChangeStatminaEvent(_currentStamina, _maxStamina);
        }
        else
        {
            Util.LogError($"{gameObject.name}: 주입된 스탯 데이터가 PlayerStat 타입과 일치하지 않음");
        }
    }
    private void StartStaminaRegenLoop()
    {
        if (_poiseCts != null)
        {
            StaminaRegenLoopAsync(_poiseCts.Token).Forget();
        }
    }

    /// <summary>
    /// 인스펙터에서 수정된 PlayerStat 수치를 런타임 모듈에 실시간 주입
    /// </summary>
    public void SyncRuntimeStats(PlayerInfo updatedStat)
    {
        if (updatedStat == null || PlayerMotionStats == null) return;

        PlayerInfoSource = updatedStat;
        _maxHp = updatedStat.CalculateMaxHp();
        _maxStamina = updatedStat.CalculateMaxStamina();

        PlayerMotionStats.SyncBaseValues(updatedStat);

        InvokeChangeHPEvent(_currentHp, _maxHp);
        InvokeChangeStatminaEvent(_currentStamina, _maxStamina);
    }

    public bool HasEnoughStamina(float cost) => _currentStamina >= cost;

    public void ConsumeStamina(float cost)
    {
        _currentStamina = UnityEngine.Mathf.Max(0f, _currentStamina - cost);
        _staminaRecoveryTimer = StaminaRegenDelay;

        InvokeChangeStatminaEvent(_currentStamina, _maxStamina);
    }

    public void RestoreStamina(float amount)
    {
        if (_isDead || amount <= 0f) return;

        _currentStamina = UnityEngine.Mathf.Min(_maxStamina, _currentStamina + amount);
        InvokeChangeStatminaEvent(_currentStamina, _maxStamina);
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
        ConsumeStamina(staminaCost);

        // 가드 피격 시에도 스태미나 회복 지연 타이머 갱신 ( 무한 가드 방어 )

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
        float lastPublishedStamina = _currentStamina;

        while (!token.IsCancellationRequested)
        {
            bool isCanceled = await UniTask.Delay(100, cancellationToken: token).SuppressCancellationThrow();
            if (isCanceled) return;

            if (_staminaRecoveryTimer > 0f)
            {
                _staminaRecoveryTimer -= 0.1f;
                continue;
            }

            if (_currentStamina < _maxStamina)
            {
                float finalRate = _staminaRegenRate * StaminaRegenMultiplier;
                _currentStamina = UnityEngine.Mathf.Min(_maxStamina, _currentStamina + finalRate * 0.1f);

                if (UnityEngine.Mathf.Abs(_currentStamina - lastPublishedStamina) >= 1.0f || _currentStamina >= _maxStamina)
                {
                    lastPublishedStamina = _currentStamina;
                    InvokeChangeStatminaEvent(_currentStamina, _maxStamina);
                }
            }
        }
    }

    /// <summary>
    /// 인벤토리 장비 교체 시 4부위(투구, 갑옷, 각반, 신발)의 합산 방어 스탯을 갱신
    /// </summary>
    public void UpdateArmorDefense(float totalFlatDefense, float totalAbsorption, float totalPoiseBonus)
    {
        _equippedArmorFlatDefense = totalFlatDefense;
        _equippedArmorAbsorption = UnityEngine.Mathf.Clamp(totalAbsorption, 0f, 0.95f);
        _equippedArmorPoiseBonus = totalPoiseBonus;

        // 베이스 스탯에 방어구 강인도 보너스 합산 반영
        if (PlayerInfoSource != null)
        {
            _maxPoise = PlayerInfoSource.Poise + _equippedArmorPoiseBonus;
            _currentPoise = UnityEngine.Mathf.Min(_currentPoise, _maxPoise);
        }

        InvokeChangePoiseEvent(_currentPoise, _maxPoise);
        Util.Log($"[방어구 갱신] 추가 DEF: {_equippedArmorFlatDefense}, 경감률: {_equippedArmorAbsorption * 100f:F1}%, 강인도 보너스: {_equippedArmorPoiseBonus}");
    }

    /// <summary>
    /// 인벤토리 장비 교체 시 장착된 방패의 물리 커트율 및 안정도를 실시간 갱신
    /// </summary>
    public void UpdateShieldSpec(float stability, float physicalAbsorption)
    {
        _equippedShieldStability = stability;
        _equippedShieldAbsorption = UnityEngine.Mathf.Clamp01(physicalAbsorption);
        Util.Log($"[방패 갱신] 안정성: {_equippedShieldStability}, 커트율: {_equippedShieldAbsorption * 100f:F1}%");
    }

    public void ApplyEquipmentStats(in EquipmentTotalStats stats)
    {
        _equippedArmorFlatDefense = stats.TotalFlatDefense;
        _equippedArmorAbsorption = UnityEngine.Mathf.Clamp(stats.TotalAbsorption, 0f, 0.95f);
        _equippedArmorPoiseBonus = stats.TotalPoiseBonus;
        _equippedShieldStability = stats.ShieldStability;
        _equippedShieldAbsorption = UnityEngine.Mathf.Clamp01(stats.ShieldAbsorption);

        _equippedBonusStrength = stats.BonusStrength;
        _equippedBonusDexterity = stats.BonusDexterity;
        _equippedBonusMaxHp = stats.BonusMaxHp;
        _equippedBonusMaxStamina = stats.BonusMaxStamina;
        _equippedBonusAttackPower = stats.BonusAttackPower;

        // 베이스 스탯에 보너스 수치 반영하여 Max 값 재산출
        if (PlayerInfoSource != null)
        {
            _maxHp = PlayerInfoSource.CalculateMaxHp() + _equippedBonusMaxHp;
            _currentHp = UnityEngine.Mathf.Min(_currentHp, _maxHp);

            _maxStamina = PlayerInfoSource.CalculateMaxStamina() + _equippedBonusMaxStamina;
            _currentStamina = UnityEngine.Mathf.Min(_currentStamina, _maxStamina);

            _maxPoise = PlayerInfoSource.Poise + _equippedArmorPoiseBonus;
            _currentPoise = UnityEngine.Mathf.Min(_currentPoise, _maxPoise);
        }

        PlayerMotionStats?.UpdateWeight(stats.TotalWeight);

        InvokeChangeHPEvent(_currentHp, _maxHp);
        InvokeChangeStatminaEvent(_currentStamina, _maxStamina);
        InvokeChangePoiseEvent(_currentPoise, _maxPoise);
    }

    private void InvokeChangeStatminaEvent(float current, float max)
    {
        OnStaminaChangedEvent?.Invoke(current, max);
        EventBusSystem.Publish(new PlayerStaminaChangedEvent(current, max));
    }
}
