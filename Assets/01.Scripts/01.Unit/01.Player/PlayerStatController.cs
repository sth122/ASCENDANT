using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Player 전용 확장 스탯 컴포넌트
/// Player 전용 스태미나 소모 및 회복 루프, 가드 상태에 따른 스태미나 감쇄 및 가드 브레이크 판정을 전담하는 컴포넌트
/// </summary>
public class PlayerStatController : UnitStatController
{
    #region Player 전용 런타임 가변 수치
    [Header("Player Exclusive: Stamina")]
    [SerializeField] private float _currentStamina;
    [SerializeField] private float _maxStamina;
    [SerializeField] private float _staminaRegenRate = 35.0f;

    // 스태미나 자연 회복 속도 배율
    public float StaminaRegenMultiplier { get; set; } = 1.0f;
    public float CurrentStamina => _currentStamina;
    public float MaxStamina => _maxStamina;
    #endregion

    public PlayerStat PlayerStatSource { get; private set; }

    public override void Init(UnitBaseStat baseStat)
    {
        base.Init(baseStat);

        if (baseStat is PlayerStat playerStat)
        {
            PlayerStatSource = playerStat;
            _maxStamina = playerStat.CalculateMaxStamina();
            _currentStamina = _maxStamina;
        }
        StaminaRegenLoopAsync(_cts.Token).Forget();
    }

    public bool HasEnoughStamina(float cost) => _currentStamina >= cost;

    public void ConsumeStamina(float cost)
    {
        _currentStamina = Mathf.Max(0f, _currentStamina - cost);
    }

    /// <summary>
    /// 가드 시 데미지 블록 처리
    /// </summary>
    /// <param name="incomingDamage">적 공격 데미지</param>
    /// <param name="shieldStability">방패 버티기(감쇄력) 수치</param>
    /// <returns>스태미나가 전부 소모하여 가드 브레이크 발생 시 true</returns>
    public bool BlockAttackWithStamina(float incomingDamage, float shieldStability)
    {
        float staminaCost = incomingDamage * (1.0f - Mathf.Clamp01(shieldStability));
        _currentStamina -= staminaCost;

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
            await UniTask.Yield(PlayerLoopTiming.Update, token);

            if(_currentStamina < _maxStamina)
            {
                _currentStamina = Mathf.Min(_maxStamina, _currentStamina + _staminaRegenRate * Time.deltaTime);
            }
        }
    }
}
