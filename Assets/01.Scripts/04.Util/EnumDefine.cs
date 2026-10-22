#region Unit Enum
public enum UnitState
{
    // 공통 또는 Player 상태
    Idle,
    Move,
    Sprint,
    Jump,
    Attack,
    Parry,
    ParryStagger,
    Stun,
    Roll,
    Riposte,
    GetHit,
    Die,

    // 에너미 공통 확장 상태
    Chase,      // 플레이어 추격
    Return,     // 활동 반경 이탈 시 스폰 지점 복귀

    // 크리처 전용 특화 상태
    Patrol,      // 포복 이동 순찰
    Alert,      // 발소리 감지 기상 및 수색
    Roar        // 발견 표효 및 동료 호출
}

public enum UnitId
{
    // 고유 0번은 Player
    Player,

    // 11번부터 각 Enemy의 고유 명사
    NightmareCreature = 11,

    // 101번부터 BossEnemy의 고유 명사
    Dragon = 101,

    // 201번부터 NPC의 고유 명사
    NPC = 201
}

public enum EnemyTier 
{
    Normal,
    Elite,
    Boss
}

#endregion

#region Stat Enum
public enum StatModType
{
    Flat,
    PercentMult,
}

#endregion

#region Input Command Type Enum
public enum InputCommandType
{
    None = 0,
    Jump,
    Roll,
    Attack,
    Parry
}
#endregion

#region AnimEnum
public enum AnimParamType
{
    MoveX,          // Float: -1(좌) ~ 1(우)
    MoveZ,          // Float: -1(후) ~ 1(전)
    StrafeX,        // Float: -1(좌) ~ 1(우) 가드 시
    StrafeZ,        // Float: -1(후) ~ 1(전) 가드 시
    IsLockOn,       // Bool: 락온 활성화 여부
    Attack,         // Trigger: 공격 트리거
    ComboStep,      // Int: 콤보 단계 (1, 2, 3)
    Riposte,        // Trigger: 그로기 처형(앞잡)
    IsGuarding,     // Bool: 가드 유지
    Parry,          // Trigger: 패링 쳐내기
    ParrySuccess,   // Trigger: 패링 성공 반격
    GetHit,         // Trigger: 피격
    Die,            // Trigger: 사망
    Jump,           // Trigger: 점프
    Roll,           // Trigger: 전방/방향 구르기
}

public enum AnimStateType
{
    // 플레이어 및 공통
    Idle,
    Move,
    Stun,

    // Enemy Locomtion 공통
    BattleIdle,
    Walk,
    Run,

    // 공격 패턴
    Attack1,
    Attack2,
    Attack3,
    Bite,
    PowerAttack,

    // 크리처 전용 특수 상태
    Crawl,
    CrawlIdle,
    CrawlToState,
    StateToCrawl,
    Roar,

    // 리액션
    Hit1 = 100,
    Hit2 = 101,

    // 사망
    Death1 = 200,
    Death2 = 201
}
#endregion

#region UI Indentifier Enum
/// <summary>
/// 프로젝트 전역에서 사용되는 UI 식별자
/// </summary>
public enum UIId
{
    None = 0,
    TitleMenuView = 1,


    // HUD 계열 (Layer: HUD)
    PlayerHUD = 10,
    BossHUD = 11,
    FloatingHpBar = 12,

    // 윈도우/패널 계열 (Layer: Window, ESC 스택 제어)
    InventoryWindow = 100,
    EquipmentWindow = 101,
    PauseWindow = 102,

    // 팝업 계열 (Layer: Popup, 모달 대화상자)
    ConfirmationPopup = 200,
    RewardPopup = 201,

    // 시스템 계열 (Layer: System)
    LoadingScreen = 300,
    FadeScreen = 301
}

/// <summary>
/// Canvas 렌더링 우선 순위 레이어
/// </summary>
public enum UILayer 
{
    Background = 0,
    HUD = 100,
    Window = 200,
    Popup = 300,
    System = 400,
}
#endregion

#region Pool Enum
/// <summary>
/// ObjectPool에서 관리하는 풀링 객체 식별자
/// </summary>
public enum PoolType
{
    None = 0,

    // 타격 및 환경 VFX (1000 ~)
    HitVFX_Slash = 1000,
    HitVFX_Blunt = 1001,
    HitVFX_Parry = 1002,
    BloodSplatter = 1003,

    // 투사체 및 인게임 드랍 (2000 ~)
    SoulOrb = 2000,

    // 풀링 UI (3000 ~)
    DamageText = 3000,
    EnemyFloatingHpBar = 3010,

    // 소환 몬스터 (4000 ~)
    NightmareCreature = 4000
}
#endregion

#region Scene Indetifier
public enum SceneId
{
    None = 0,
    TitleScene = 1,
    GameScene = 2,
    BossScnen = 3,
}
#endregion