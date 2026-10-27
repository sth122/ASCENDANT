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

    [Header("코드 타임라인 정규화 시간 (0.0 ~ 1.0)")]
    [Tooltip("히트박스가 열리는 애니메이션 진행도 (기본 0.25)")]
    [SerializeField] private float _hitboxStartNormalized;
    [Tooltip("히트박스가 닫히는 애니메이션 진행도 (기본 0.60)")]
    [SerializeField] private float _hitboxEndNormalized;
    [Tooltip("공격 후딜레이가 끝나고 다음 상태로 전이 가능한 진행도 (기본 0.90)")]
    [SerializeField] private float _actionEndNormalized;

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

    public float HitboxStartNormalized => _hitboxStartNormalized >= 0f ? _hitboxStartNormalized : 0.25f;
    public float HitboxEndNormalized => _hitboxEndNormalized > 0f ? _hitboxEndNormalized : 0.60f;
    public float ActionEndNormalized => _actionEndNormalized > 0f ? _actionEndNormalized : 0.90f;
    #endregion

    public EnemyAttackData(AnimStateType animState, float damageMultiplier, float poiseMultiplier, float range, 
        float cooldown, bool isParryable, float hitboxStart = 0.25f, float hitboxEnd = 0.60f, float actionEnd = 0.90f)
    {
        _attackAnimStateType = animState;
        _damageMultiplier = damageMultiplier;
        _poiseMultiplier = poiseMultiplier;
        _attackRange = range;
        _attackCooldown = cooldown;
        _isParryable = isParryable;
        _hitboxStartNormalized = hitboxStart;
        _hitboxEndNormalized = hitboxEnd;
        _actionEndNormalized = actionEnd;
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
