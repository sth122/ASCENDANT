using UnityEngine;

/// <summary>
/// 플레이어 액션별 스태미나 소모량 및 프레임 타이밍 설정 SO
/// </summary>
[CreateAssetMenu(fileName = "PlayerCombatConfig", menuName = "Combat/PlayerCombatConfigSO")]
public class PlayerCombatConfigSO : ScriptableObject
{
    [Header("스태미나 소모 비용")]
    [SerializeField] private float _attackStaminaCost = 14.0f;
    [SerializeField] private float _rollStaminaCost = 18.0f;
    [SerializeField] private float _jumpStaminaCost = 12.0f;
    [SerializeField] private float _sprintStaminaCostPerSec = 16.0f;
    [SerializeField] private float _minimumSprintStamina = 5.0f;

    [Header("구르기(회피) 타이밍")]
    [SerializeField] private float _rollInvincibleDuration = 0.35f;
    [SerializeField] private float _rollTotalDuration = 0.85f;
    [SerializeField] private float _rollRecoveryCancelWindow = 0.65f;

    [Header("가드 및 패링")]
    [SerializeField] private float _parryWindowDuration = 0.2f;
    [SerializeField] private float _guardMoveSpeedFactor = 0.75f;
    [SerializeField] private float _guardStaminaRegenFactor = 0.3f;
    [SerializeField] private float _shieldStability = 0.7f;
    [SerializeField] private float _guardBlockAngle = 120.0f;

    [Header("중량비 구르기 티어별 타이밍 테이블 (Data-Driven)")]
    [SerializeField] private RollTierConfig _fastRollConfig = new RollTierConfig(14f / 60f, 22f / 60f, 18f / 60f);
    [SerializeField] private RollTierConfig _midRollConfig = new RollTierConfig(12f / 60f, 25f / 60f, 21f / 60f);
    [SerializeField] private RollTierConfig _fatRollConfig = new RollTierConfig(8f / 60f, 32f / 60f, 28f / 60f);

    #region Properties
    public float AttackStaminaCost => _attackStaminaCost;
    public float RollStaminaCost => _rollStaminaCost;
    public float JumpStaminaCost => _jumpStaminaCost;
    public float SprintStaminaCostPerSec => _sprintStaminaCostPerSec;
    public float MinimumSprintStamina => _minimumSprintStamina;

    public float RollInvincibleDuration => _rollInvincibleDuration;
    public float RollTotalDuration => _rollTotalDuration;
    public float RollRecoveryCancelWindow => _rollRecoveryCancelWindow;

    public float ParryWindowDuration => _parryWindowDuration;
    public float GuardMoveSpeedFactor => _guardMoveSpeedFactor;
    public float GuardStaminaRegenFactor => _guardStaminaRegenFactor;
    public float ShieldStability => _shieldStability;
    public float GuardBlockAngle => _guardBlockAngle;
    #endregion

    public RollTierConfig GetRollTierConfig(RollTier tier)
    {
        return tier switch
        {
            RollTier.Fast => _fastRollConfig,
            RollTier.Mid => _midRollConfig,
            _ => _fatRollConfig
        };
    }
}

[System.Serializable]
public struct RollTierConfig
{
    [Tooltip("무적 판정(i-Frame) 지속 시간 (초)")]
    public float InvincibleDuration;
    [Tooltip("전체 구르기 모션 동작 시간 (초)")]
    public float TotalDuration;
    [Tooltip("이동 캔슬 복귀 가능 시점 (초)")]
    public float CancelWindow;

    public RollTierConfig(float iFrame, float total, float cancel)
    {
        InvincibleDuration = iFrame;
        TotalDuration = total;
        CancelWindow = cancel;
    }
}