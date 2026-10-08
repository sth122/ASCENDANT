using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 플레이어 전용 확장 스탯 컴포넌트입니다.
/// 달리기, 점프, 구르기, 장비 중량, 육성 어트리뷰트(Vigor, Endurance 등)와 같은
/// 플레이어만의 특화 스탯을 인스펙터에 단독으로 노출하고 실시간 튜닝(Live Tuning)을 지원합니다.
/// </summary>
public class PlayerStatController : UnitStatController
{
    [Header("Player Exclusive: Stamina")]
    [SerializeField] private float _currentStamina;
    [SerializeField] private float _maxStamina;
    [SerializeField] private float _staminaRegenRate = 35.0f;

    public float CurrentStamina => _currentStamina;
    public float MaxStamina => _maxStamina;
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
