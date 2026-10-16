using UnityEngine;

public interface IInitable
{
    public void Init();
}

// FSM  상태 인터페이스
public interface IState
{
    public void Enter();
    public void Exit();
    public void Update();
    public void FixedUpdate();
}

/// <summary>
/// 무기 궤적 및 타격 판정 콜라이더 개폐 이벤트 인터페이스 (모든 유닛 공통)
/// </summary>
public interface IAnimationHitBoxEvent
{
    void Anim_OnHitboxOpen();
    void Anim_OnHitboxClose();
}

/// <summary>
/// 단일 애니메이션 액션 완료 시점 수신 인터페이스 (공격, 스턴, 기상 등 범용)
/// </summary>
public interface IAnimationActionEvent
{
    void Anim_OnActionCompleted();
}

/// <summary>
/// 연속 공격 캔슬 및 선입력 콤보 윈도우 통제 인터페이스 (플레이어 전용)
/// </summary>
public interface IAnimationCombatEvent
{
    void Anim_OnComboWindowOpen();
    void Anim_OnComboWindowClose();
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