public interface IEventType { }

/// <summary>
/// Unit 체력 변동 이벤트 (HUD UI 바인딩용)
/// </summary>
public readonly struct UnitHpChangedEvent : IEventType
{
    public readonly UnityEngine.GameObject TargetUnit;
    public readonly float CurrentHp;
    public readonly float MaxHp;

    public UnitHpChangedEvent(UnityEngine.GameObject targetUnit, float currentHp, float maxHp)
    {
        TargetUnit = targetUnit;
        CurrentHp = currentHp;
        MaxHp = maxHp;
    }
}

/// <summary>
/// Unit Poise(강인도) 변동 이벤트
/// </summary>
public readonly struct UnitPoiseChangedEvent : IEventType
{
    public readonly UnityEngine.GameObject TargetUnit;
    public readonly float CurrentPoise;
    public readonly float MaxPoise;

    public UnitPoiseChangedEvent(UnityEngine.GameObject targetUnit, float currentPoise, float maxPoise)
    {
        TargetUnit = targetUnit;
        CurrentPoise = currentPoise;
        MaxPoise = maxPoise;
    }
}

/// <summary>
/// Player 스태미나 변동 이벤트 (HUD 전용)
/// </summary>
public readonly struct PlayerStaminaChangedEvent : IEventType
{
    public readonly float CurrentStamina;
    public readonly float MaxStamina;

    public PlayerStaminaChangedEvent(float currentStamina, float maxStamina)
    {
        CurrentStamina = currentStamina;
        MaxStamina = maxStamina;
    }
}

/// <summary>
/// Unit 사망 이벤트
/// </summary>
public readonly struct UnitDiedEvent : IEventType
{
    public readonly UnityEngine.GameObject DeadUnit;
    public readonly UnitId Identifier;

    public UnitDiedEvent(UnityEngine.GameObject deadUnit, UnitId identifier)
    {
        DeadUnit = deadUnit;
        Identifier = identifier;
    }
}

/// <summary>
/// 락온 대상 타겟 변경 이벤트 (카메라 및 UI 조준점 연동용)
/// </summary>
public readonly struct LockOnTargetChangedEvent : IEventType
{
    public readonly UnityEngine.Transform TargetTransform;
    public readonly bool IsLockedOn;

    public LockOnTargetChangedEvent(UnityEngine.Transform targetTransform, bool isLockedOn)
    {
        TargetTransform = targetTransform;
        IsLockedOn = isLockedOn;
    }
}
