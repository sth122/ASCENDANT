using UnityEngine;
/// <summary>
/// Enemy의 개별 공격 패턴을 정의하는 구조체
/// </summary>
[System.Serializable]
public struct EnemyAttackData
{
    [Header("공격 식별")]
    [Tooltip("AnimationDataSO에 등록된 재생 대상 애니메이션 상태 Enum")]
    [field: SerializeField] private AnimStateType _attackAnimStateType;

    [Header("전투 모션 배율 (Motion Value Multipliers)")]
    [Tooltip("모션 피해 배율")]
    [SerializeField] private float _damageMultiplier;
    [Tooltip("강인도 피해 배율")]
    [SerializeField] private float _poiseMultiplier;

    [Header("사거리 및 쿨타임")]
    [SerializeField] private float _attackRange;
    [SerializeField] private float _attackCooldown;

    [Header("소울라이크 패링 메타데이터")]
    [Tooltip("패링이 허용되는 일반 패턴인지 여부")]
    [SerializeField] private bool _isParryable;

    #region Properties
    public AnimStateType AttackAnimStateType => _attackAnimStateType;
    public float DamageMultiplier => _damageMultiplier;
    public float PoiseMultiplier => _poiseMultiplier;
    public float AttackRange => _attackRange;
    public float AttackCooldown => _attackCooldown;
    public bool IsParryable => _isParryable;
    #endregion

    public EnemyAttackData(AnimStateType animState, float damageMultiplier, float poiseMultiplier, float range, float cooldown, bool isParryable)
    {
        _attackAnimStateType = animState;
        _damageMultiplier = damageMultiplier;
        _poiseMultiplier = poiseMultiplier;
        _attackRange = range;
        _attackCooldown = cooldown;
        _isParryable = isParryable;
    }

    /// <summary>
    /// 유닛의 기준 스탯과 모션 배율을 결합하여 최종 판정 피해량을 산출하는 연산 메서드
    /// </summary>
    public float CalculateFinalDamage(float baseAttackPower)
        => baseAttackPower * _damageMultiplier;

    /// <summary>
    /// 유닛의 기준 강인도 공격력과 모션 배율을 결합하여 최종 강인도 피해량을 산출하는 연산 메서드
    /// </summary>
    public float CalculateFinalPoiseDamage(float basePoiseDamage) 
        => basePoiseDamage * _poiseMultiplier;
}
