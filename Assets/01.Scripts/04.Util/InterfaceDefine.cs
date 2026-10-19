using UnityEngine;

public interface IInitable
{
    public void Init();
}


/// <summary>
/// FSM  상태 인터페이스
/// </summary>
public interface IState
{
    public void Enter();
    public void Exit();
    public void Update();
    public void FixedUpdate();
}

/// <summary>
/// 공격 및 타격 시 전달되는 순수 데이터 컨텍스트 구조체
/// </summary>
public readonly struct DamageContext
{
    public readonly GameObject Attacker;
    public readonly float Damage;
    public readonly float PoiseDamage;
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitNormal;
    public readonly bool IsParryable;

    public DamageContext(GameObject attacker, float damage, float poiseDamage, Vector3 hitPoint, Vector3 hitNormal, bool isParryable = true)
    {
        Attacker = attacker;
        Damage = damage;
        PoiseDamage = poiseDamage;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        IsParryable = isParryable;
    }
}

/// <summary>
/// 공격 수신 인터페이스 (플레이어, 몬스터, 보스 공통)
/// </summary>
public interface IDamageable
{
    void ApplyDamage(in DamageContext context);
}

/// <summary>
/// Enemy 공격 실행 전략 인터페이스
/// </summary>
public interface IEnemyAttackStrategy
{
    bool CanExecute(in EnemyAttackContext context, Transform target, in EnemyAttackData data);
    void Execute(in EnemyAttackContext context, Transform target, in EnemyAttackData data);
    void Update(in EnemyAttackContext context, Transform target, in EnemyAttackData data, float normalizedTime);
    void Stop(in EnemyAttackContext context);
}