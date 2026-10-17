using UnityEngine;

public struct EnemyAttackData
{
    [Header("공격 식별")]
    [Tooltip("AnimationDataSO에 등록된 재생 대상 애니메이션 상태 Enum")]
    [field: SerializeField] private AnimStateType _attackAnimStateType;

    [Header("전투 판정 수치")]
    [field: SerializeField] private float _attackDamage;
    [field: SerializeField] private float _poiseDamage;
    [field: SerializeField] private float _attackRange;
    [field: SerializeField] private float _attackCooldown;

    [Header("소울라이크 패링 메타데이터")]
    [Tooltip("플레이어의 Parry가 허용되는 일반 공격인지 여부")]
    [field: SerializeField] private bool _isParryable;

    #region Properties
    public AnimStateType AttackAnimStateType => _attackAnimStateType;
    public float AttackDamage => _attackDamage;
    public float PoiseDamage => _poiseDamage;
    public float AttackRange => _attackRange;
    public float AttackCooldown => _attackCooldown;
    public bool IsParryable => _isParryable;
    #endregion

    public EnemyAttackData(AnimStateType animState, float damage, float poiseDamage, float range, float cooldown, bool isParryable)
    {
        _attackAnimStateType = animState;
        _attackDamage = damage;
        _poiseDamage = poiseDamage;
        _attackRange = range;
        _attackCooldown = cooldown;
        _isParryable = isParryable;
    }
}
