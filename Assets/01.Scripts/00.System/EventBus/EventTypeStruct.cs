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

/// <summary>
/// 플레이어가 화톳불(세이브 터미널)에서 휴식을 취했을 때 발생하는 월드 리셋 이벤트
/// </summary>
public readonly struct BonfireRestedEvent : IEventType
{
    public readonly UnityEngine.Vector3 BonfirePosition;

    public BonfireRestedEvent(UnityEngine.Vector3 bonfirePosition)
    {
        BonfirePosition = bonfirePosition;
    }
}

/// <summary>
/// GameManager 상태 변동 알림 이벤트
/// </summary>
public readonly struct GameStateChangedEvent : IEventType
{
    public readonly GameState PreviousState;
    public readonly GameState NewState;

    public GameStateChangedEvent(GameState previousState, GameState newState)
    {
        PreviousState = previousState;
        NewState = newState;
    }
}

/// <summary>
/// 씬 부트스트래퍼의 모든 시스템, 데이터, 플레이어 스폰 완료를 전역 신호
/// 씬에 정적으로 배치된 에너미와 시스템 액터들은 이 이벤트를 수신한 후 로직을 기동
/// </summary>
public readonly struct GameSceneReadyEvent : IEventType
{
    public readonly SceneId Scene;
    public GameSceneReadyEvent(SceneId scene) => Scene = scene;
}

public readonly struct InteractionPromptEvent : IEventType
{
    public readonly bool Show;
    public readonly string PromptText;

    public InteractionPromptEvent(bool show, string promptText)
    {
        Show = show;
        PromptText = promptText;
    }
}

/// <summary>
/// 보스 조우 시점 보스 HUD를 활성화하는 이벤트
/// </summary>
public readonly struct BossEncounterEvent : IEventType
{
    public readonly string BossName;
    public readonly UnityEngine.GameObject BossObject;
    public readonly float InitialHp;
    public readonly float MaxHp;
    public readonly float InitialPoise;
    public readonly float MaxPoise;

    public BossEncounterEvent(string bossName, UnityEngine.GameObject bossObject, float initialHp, float maxHp, float initialPoise, float maxPoise)
    {
        BossName = bossName;
        BossObject = bossObject;
        InitialHp = initialHp;
        MaxHp = maxHp;
        InitialPoise = initialPoise;
        MaxPoise = maxPoise;
    }
}

/// <summary>
/// 보스 처치 또는 전투 이탈 시 보스 HUD를 비활성화하는 이벤트
/// </summary>
public readonly struct BossDefeatedEvent : IEventType
{
    public readonly UnityEngine.GameObject BossObject;

    public BossDefeatedEvent(UnityEngine.GameObject bossObject)
    {
        BossObject = bossObject;
    }
}

/// <summary>
/// 씬 로딩 진행도 및 안내 문구 갱신 이벤트
/// </summary>
public readonly struct SceneLoadingProgressEvent : IEventType
{
    public readonly float Progress; // 0.0f ~ 1.0f
    public readonly string Description;

    public SceneLoadingProgressEvent(float progress, string description = "")
    {
        Progress = UnityEngine.Mathf.Clamp01(progress);
        Description = description;
    }
}


/// <summary>
/// 유닛 무적 갱신 이벤트
/// </summary>
public readonly struct UnitInvincibleChangedEvent : IEventType
{
    public readonly UnityEngine.GameObject TargetUnit;
    public readonly bool IsInvincible;

    public UnitInvincibleChangedEvent(UnityEngine.GameObject targetUnit, bool isInvincible)
    {
        TargetUnit = targetUnit;
        IsInvincible = isInvincible;
    }
}

/// <summary>
/// 장비 장착/해제 및 스탯 계산이 완료되어 최대 능력치가 확정되었을 때 1회 발행되는 이벤트
/// </summary>
public readonly struct PlayerMaxStatsCalculatedEvent : IEventType
{
    public readonly float MaxHp;
    public readonly float MaxStamina;
    public readonly float MaxPoise;

    public PlayerMaxStatsCalculatedEvent(float maxHp, float maxStamina, float maxPoise)
    {
        MaxHp = maxHp;
        MaxStamina = maxStamina;
        MaxPoise = maxPoise;
    }
}

/// <summary>
/// 플레이어가 달리기(Sprint) 중 발소리를 냈을 때 발행되는 청각 감지 이벤트
/// </summary>
public readonly struct FootstepNoiseEvent : IEventType
{
    public readonly UnityEngine.GameObject SourceUnit;
    public readonly UnityEngine.Vector3 NoisePosition;

    public FootstepNoiseEvent(UnityEngine.GameObject sourceUnit, UnityEngine.Vector3 noisePosition)
    {
        SourceUnit = sourceUnit;
        NoisePosition = noisePosition;
    }
}

/// <summary>
/// 플레이어 소지 소울 변동 이벤트 (HUD 및 인벤토리 실시간 동기화용)
/// </summary>
public readonly struct SoulChangedEvent : IEventType
{
    public readonly int CurrentSouls;
    public readonly int DeltaAmount;

    public SoulChangedEvent(int currentSouls, int deltaAmount)
    {
        CurrentSouls = currentSouls;
        DeltaAmount = deltaAmount;
    }
}

/// <summary>
/// 세이브 파일 기록 진행 시 우측 하단에 텍스트 출력 이벤트
/// </summary>
public readonly struct GameSavingEvent : IEventType
{
    public readonly int SlotIndex;
    public GameSavingEvent(int slotIndex) => SlotIndex = slotIndex;
}
/// <summary>
/// 세이브 파일 기록 완료 시 우측 하단에 텍스트 출력 이벤트
/// </summary>
public readonly struct GameSavedEvent : IEventType
{
    public readonly int SlotIndex;
    public GameSavedEvent(int slotIndex) => SlotIndex = slotIndex;
}

/// <summary>
/// 타이틀 인트로 UI 표시 여부 제어 이벤트
/// </summary>
public readonly struct TitleIntroVisibilityEvent : IEventType
{
    public readonly bool IsVisible;
    public TitleIntroVisibilityEvent(bool isVisible) => IsVisible = isVisible;
}