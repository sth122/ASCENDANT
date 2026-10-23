#region 시스템 및 씬 수명 주기
/// <summary>
/// 씬 식별자 (Addressables Scene Name 및 로딩 파이프라인 매핑)
/// </summary>
public enum SceneId
{
    None,
    TitleScene,
    GameScene,
    BossScene
}

/// <summary>
/// ServiceLocator 바인딩 인스턴스의 수명 주기
/// </summary>
public enum ServiceLifetime
{
    Global, // 씬 전환과 무관하게 영구 유지
    Local   // 씬 전환 시 ClearSceneLocalServices()에 의해 일괄 소멸
}

/// <summary>
/// 게임의 거시적 실행 상태 (GameManager 제어용)
/// </summary>
public enum GameState
{
    None,
    Title,      // 타이틀 화면
    Playing,    // 인게임 정상 플레이
    Paused,     // ESC 시스템 창, 인벤토리 등 일시정지 상태
    Cinematic,  // 컷신/보스 연출 재생 중
    GameOver    // 플레이어 사망
}
#endregion

#region Unit 및 Input 판정
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
public enum InputCommandType
{
    None = 0,
    Jump,
    Roll,
    Attack,
    Parry
}
#endregion

#region Unit FSM State
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
    Roar,        // 발견 표효 및 동료 호출
    RoarResponse // 발견 표호 답변
}
#endregion

#region Object Pooling
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

#region Stat Enum
public enum StatModType
{
    Flat,
    PercentMult,
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

    // 1000 ~ 1999 : Background & Scene (배경 및 전체화면 씬 UI)
    TitleBackgroundUI = 1001,     // 타이틀 아트/배경 연출
    GameOverBackgroundUI = 1002,     // 게임오버 배경 연출


    // 2000 ~ 2999 : HUD (인게임 상시 오버레이 및 플로팅 게이지)
    PlayerHUD = 2001,     // 플레이어 HP/스태미나/Poise 3-Bar HUD
    EnemyFloatingHUD = 2002,     // 일반 몬스터 머리 위 플로팅 게이지
    BossHUD = 2003,     // 화면 하단 보스 전용 대형 체력바


    // 3000 ~ 3999 : Window (ESC 스택으로 제어되며 입력을 점유하는 독립 창)
    TitleMenuUI = 3001,     // 타이틀 메뉴 버튼 창 (새 게임, 이어하기, 설정)
    SystemMenuUI = 3002,     // 인게임 ESC 일시정지 시스템 창
    InventoryUI = 3003,     // 인벤토리 창 (Tab/I 토글)
    EquipmentUI = 3004,     // 장비 장착/변경 창
    StatusUI = 3005,     // 캐릭터 상세 스탯 정보 창


    // 4000 ~ 4999 : Popup (비동기 결과 대기 및 확인/경고 모달 다이얼로그)
    ConfirmPopup = 4001,     // 확인/취소 모달 팝업
    NoticePopup = 4002,     // 단일 확인 알림 모달 팝업
    KeyBindingPopup = 4003,     // 키 매핑 변경 팝업


    // 5000 ~ 5999 : System (최상위 화면 제어, 화면 전환 및 시스템 인디케이터)
    FadeCurtainUI = 5001,     // 씬 전환 암전용 페이드 디머
    LoadingScreenUI = 5002,     // 비동기 씬 로딩 스크린 프로그레스바
}

/// <summary>
/// Canvas 렌더링 우선 순위 레이어
/// </summary>
public enum UILayer 
{
    Background = 0,     // 배경/타이틀
    World,              // 월드 스페이스 UI
    HUD = 100,          // HUD
    Window = 200,       // 인벤토리, 장비창, 시스템 메뉴 (ESC 스택 관리)
    Popup = 300,        // 팝업
    System = 400,       // 로딩 뷰, 페이드 딤
}
#endregion