using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Enemy의 개별 공격 패턴을 정의하는 구조체
/// 단일 타격 및 ActionTimelineStep 기반 다단 연계 시퀀스
/// </summary>
[System.Serializable]
public struct EnemyAttackData
{
    [Header("공격 식별")]
    [SerializeField] private AnimStateType _attackAnimStateType;

    [Header("충격 판정 속성")]
    [SerializeField] private AttackImpactType _impactType;

    [Header("사거리 및 쿨타임")]
    [SerializeField] private float _attackRange;
    [SerializeField] private float _attackCooldown;

    [Header("단일 공격 정규화 타임라인 (0.0 ~ 1.0)")]
    [SerializeField] private float _hitboxStartNormalized;
    [SerializeField] private float _hitboxEndNormalized;

    [Header("복합 타임라인 스텝 (비어있으면 위 단일 설정값으로 1-Step 자동 구성)")]
    [SerializeField] private List<ActionTimelineStep> _customSteps;

    [Header("소울라이크 패링 및 넉백")]
    [Tooltip("패링이 허용되는 일반 패턴인지 여부")]
    [SerializeField] private bool _isParryable;
    [Tooltip("강공격 적중 시 플레이어를 밀어내는 임펄스 넉백 힘")]
    [SerializeField] private float _knockbackForce;

    #region Properties
    public AnimStateType AttackAnimStateType => _attackAnimStateType;
    public AttackImpactType ImpactType => _impactType;
    public float AttackRange => _attackRange > 0f ? _attackRange : 2.2f;
    public float AttackCooldown => _attackCooldown >= 0f ? _attackCooldown : 2.0f;
    public bool IsParryable => _isParryable;
    public float KnockbackForce => _knockbackForce;

    public float HitboxStartNormalized => _hitboxStartNormalized >= 0f ? _hitboxStartNormalized : 0.25f;
    public float HitboxEndNormalized => _hitboxEndNormalized > 0f ? _hitboxEndNormalized : 0.60f;
    /// <summary>
    /// AttackImpactType에 따라 자동으로 부여되는 대미지 계수
    /// </summary>
    public float AutoDamageMultiplier => _impactType switch
    {
        AttackImpactType.Light => 1.0f,
        AttackImpactType.Heavy => 1.8f,
        AttackImpactType.DoT => 0.4f,
        _ => 1.0f
    };

    /// <summary>
    /// AttackImpactType에 따라 자동으로 부여되는 강인도 파괴 계수
    /// </summary>
    public float AutoPoiseMultiplier => _impactType switch
    {
        AttackImpactType.Light => 1.0f,
        AttackImpactType.Heavy => 2.2f,
        AttackImpactType.DoT => 0.0f,
        _ => 1.0f
    };
    #endregion

    public EnemyAttackData(AnimStateType animState, AttackImpactType impactType, float range, float cooldown,
        bool isParryable, float knockbackForce = 0f, float hitboxStart = 0.25f, float hitboxEnd = 0.60f)
    {
        _attackAnimStateType = animState;
        _impactType = impactType;
        _attackRange = range;
        _attackCooldown = cooldown;
        _isParryable = isParryable;
        _knockbackForce = knockbackForce;
        _hitboxStartNormalized = hitboxStart;
        _hitboxEndNormalized = hitboxEnd;
        _customSteps = null;
    }

    /// <summary>
    /// 유닛의 기준 공격력에 ImpactType 자동 배율을 결합하여 최종 대미지 산출
    /// </summary>
    public float CalculateFinalDamage(float baseAttackPower) => baseAttackPower * AutoDamageMultiplier;

    /// <summary>
    /// 유닛의 기준 포이즈 공격력에 ImpactType 자동 배율을 결합하여 최종 강인도 피해량 산출
    /// </summary>
    public float CalculateFinalPoiseDamage(float basePoiseDamage) => basePoiseDamage * AutoPoiseMultiplier;

    /// <summary>
    /// ActionTimelineRunner에 주입할 유효 Step 배열 반환
    /// </summary>
    public ActionTimelineStep[] GetTimelineSteps()
    {
        if (_customSteps != null && _customSteps.Count > 0)
        {
            return _customSteps.ToArray();
        }

        return new ActionTimelineStep[]
        {
            ActionTimelineStep.CreateSingleAttack(
                _attackAnimStateType,
                HitboxSlot.MainWeapon,
                HitboxStartNormalized,
                HitboxEndNormalized
            )
        };
    }
}
