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
    Guard
}
#endregion

#region Unit FSM State
public enum UnitState
{
    // Player 상태
    // 플레이어 기본 로코모션
    Idle,
    Move,
    Sprint,
    Jump,
    Fall,           // 공중 낙하 체공
    HardLanding,    // 고낙차 착지 경직

    // 전투 액션
    Attack,
    Guard,          // 가드 유지 및 Strafe 이동
    Parry,          // 패링 쳐내기 액션
    Riposte,        // 처형(앞잡)
    UseItem,        // 아이템 사용
    Roll,

    // 피격 및 무력화
    GetHit,
    GuardBreak,     // 스태미나 부족 방패 파쇄
    Groggy,         // 강인도 완전 소진 시 무력화 대기 (처형 허용)
    Knockback,
    Die,
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

    VFX_Breath_Fire = 1501,       // FlameThrower
    VFX_EarthShatter = 1502,      // EarthShatter
    VFX_MeteorExplosion = 1503,   // 메테오 착탄 폭발

    // 투사체 및 인게임 드랍 (2000 ~)
    SoulOrb = 2000,
    MeteorProjectile = 2001,
    FireBomb = 2002,

    // 풀링 UI (3000 ~)
    DamageText = 3000,
    EnemyFloatingHpBar = 3010,

    // 소환 몬스터 (4000 ~)
    NightmareCreature = 4000,

    // 보스 몬스터 (6000 ~)
    BossDragon = 6000,
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
    GuardImpact,    // Trigger: 방패로 적의 공격을 가드(방어)했을 때 밀려나는 모션
    Knockback,      // Trigger: 강공격/패링불가 공격에 맞아 바닥으로 날아가는 넉다운 모션
    WakeUp,         // Trigger: 넘어진 바닥에서 일어나는 기상 모션
    GuardBreak,     // Trigger: 스태미나 고갈로 방패가 젖혀지는 가드 파쇄 모션
    Groggy,         // Trigger: 그로기 무력화 진입 모션
    GetHit,         // Trigger: 피격
    Die,            // Trigger: 사망
    Jump,           // Trigger: 점프
    Roll,           // Trigger: 전방/방향 구르기
    Fall,           // Trigger: 공중 낙하 체공 모션
    HardLanding,    // Trigger: 하드 랜딩 착지 경직 모션
    UseItem         // Trigger: 소모품 사용 모션
}

public enum AnimStateType
{
    // 로코모션
    Idle = 0,
    Move = 1,
    FallLoop = 2,           // 체공 루프
    HardLanding = 3,        // 하드 랜딩 착지

    // 전투 및 방어
    Guard = 4,              // 가드 유지
    Parry = 5,              // 패링 액션
    GuardImpact = 6,        // 가드 임팩트
    GuardBreak = 7,         // 가드 파쇄
    Groggy_Loop = 8,        // 그로기 대기 루프
    Knockback = 9,          // 넉백
    WakeUp = 10,             // 넉백 기상

    // 소모품
    UsePotion = 11,          // 포션 사용
    ThrowBomb = 12,          // 투척 모션

    // 에너미 로코모션 및 복귀
    Walk = 13,
    Run = 14,
    Patrol = 15,
    PatrolIdle = 16,

    // 공격 패턴
    Attack1 = 50,
    Attack2 = 51,
    Attack3 = 52,
    Bite = 53,
    PowerAttack = 54,

    // 크리처 전용
    CrawlToState = 70,
    StateToCrawl = 71,
    Roar = 72,

    // 리액션
    Hit1 = 100,
    Hit2 = 101,

    // 사망
    Death = 200,
    Death2 = 201,

    // 드래곤 보스 전용 세부 애니메이션 클립 매핑 (1000 ~)
    Backstep                = 1000,     // 백스탭
    Dragon_LinearBreath     = 1001,     // 전방 직선 관통 브레스
    Dragon_BackstepBreath   = 1002,     // 후퇴 후 우 -> 좌 브레스
    BackstepCharge          = 1003,     // 후퇴 후 돌진
    TailSweep               = 1004,     // 360도 회전 꼬리 강타

    Dragon_HoverLoop        = 1005,     // 공중 체공 호버링 루프
    Dragon_AerialBreath_dz  = 1006,     // 공중 체공 중 하방 브레스
    Dragon_Takeoff_InPlace  = 1007,     // 제자리 도약
    Dragon_Landing_InPlace  = 1008,     // 제자리 착지
    Dragon_Takeoff_dz       = 1009,     // 백스탭 도약
    Dragon_Landing_dz       = 1010,     // 백스탭 지점 착지
    Berserk                 = 1011,     // 광폭화

    Boss_GroggyDown         = 1012,     // 보스 대경직 넉다운
    Turn_Left               = 1013,     // 제자리 좌선회 90도
    Turn_Right              = 1014      //제자리 우선회 90도
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
    TitleBackgroundUI       = 1001,     // 타이틀 아트/배경 연출
    GameOverBackgroundUI    = 1002,     // 게임오버 배경 연출


    // 2000 ~ 2999 : HUD (인게임 상시 오버레이 및 플로팅 게이지)
    PlayerHUD               = 2001,     // 플레이어 HP/스태미나/Poise 3-Bar HUD
    EnemyFloatingHUD        = 2002,     // 일반 몬스터 머리 위 플로팅 게이지
    BossHUD                 = 2003,     // 화면 하단 보스 전용 대형 체력바
    QuickSlotHUD            = 2004,     // 퀵슬롯 아이템 4개 표시 HUD

    // 3000 ~ 3999 : Window (ESC 스택으로 제어되며 입력을 점유하는 독립 창)
    TitleMenuUI             = 3001,     // 타이틀 메뉴 버튼 창 (새 게임, 이어하기, 설정)
    SystemMenuUI            = 3002,     // 인게임 ESC 일시정지 시스템 창
    InventoryUI             = 3003,     // 인벤토리 창 (Tab/I 토글)
    EquipmentUI             = 3004,     // 장비 장착/변경 창
    StatusUI                = 3005,     // 캐릭터 상세 스탯 정보 창
    GameOverUI              = 3006,

    // 4000 ~ 4999 : Popup (비동기 결과 대기 및 확인/경고 모달 다이얼로그)
    ConfirmPopup            = 4001,     // 확인/취소 모달 팝업
    NoticePopup             = 4002,     // 단일 확인 알림 모달 팝업
    KeyBindingPopup         = 4003,     // 키 매핑 변경 팝업


    // 5000 ~ 5999 : System (최상위 화면 제어, 화면 전환 및 시스템 인디케이터)
    FadeCurtainUI           = 5001,     // 씬 전환 암전용 페이드 디머
    LoadingScreenUI         = 5002,     // 비동기 씬 로딩 스크린 프로그레스바
}

/// <summary>
/// Canvas 렌더링 우선 순위 레이어
/// </summary>
public enum UILayer 
{
    Background      = 0,     // 배경/타이틀
    World,                   // 월드 스페이스 UI
    HUD             = 100,   // HUD
    Window          = 200,   // 인벤토리, 장비창, 시스템 메뉴 (ESC 스택 관리)
    Popup           = 300,   // 팝업
    System          = 400,   // 로딩 뷰, 페이드 딤
}
#endregion

#region Unit Attack + Skill
public enum HitboxSlot
{
    None = 0,
    MainWeapon,        // 무기
    RightHand = 1,     // 오른손
    LeftHand = 2,      // 왼손
    BothHands = 3,     // 양손 공격
    Mouth = 4,         // 머리/입
    Tail = 5,          // 꼬리
    WholeBody = 6      // 돌진 전신 판정
}

public enum AttackImpactType
{
    Light,     // 약공격: 일반 피격 경직
    Heavy,     // 강공격: 넉백 + 2초 다운 + 1초 기상 무적
    DoT        // 장판 틱딜
}

public enum SkillId
{
    None = 0,

    // Dragon Boss Skills
    Dragon_BackstepBreath = 3001,
    Dragon_BackstepCharge = 3002,
    Dragon_TailSweep = 3003,
    Dragon_LinearBreath = 3004,
    Dragon_AerialCombo = 3005,
    Dragon_MeteorShower = 3006,
}
#endregion

public enum BlockResultType
{
    Failed,
    ParrySuccess,
    HeavyParryPushback,
    GuardImpact,
    GuardBreak,
    Knockback
}

#region Interaction
/// <summary>
/// 인게임 상호작용 객체 식별자
/// </summary>
public enum InteractType
{
    None = 0,
    Bonfire,      // 화톳불 휴식 및 저장
    BossDoor,     // 보스방 진입
    ItemDrop,     // 필드 드랍 아이템 획득
    NpcTalk,      // NPC 대화
    Chest,        // 보물상자 열기
    Lever         // 기믹 레버 조작
}
#endregion

#region Inventory + Item
public enum ItemType
{
    Countable,   // 소모품, 재료 등 수량성 아이템
    Equipment    // 무기, 방패, 방어구 등 장비 아이템
}

public enum PortionEffectType
{
    HealHp,          // HP 회복
    ThrowFireBomb    // 투척 화염병
}

public enum EquipSlotType
{
    MainWeapon, // 무기
    Shield,     // 방패
    Helmet,     // 머리 투구
    ChestArmor, // 상체 갑옷
    Leggings,   // 하체 각반
    Boots       // 발 신발
}

public enum ShieldClassification
{
    Small, Medium 
}

public enum ItemCategory
{
    Consumable, 
    Weapon,     
    Shield,     
    Armor       
}

public enum StatScalingGrade
{
    None = 0,
    E = 1,
    D = 2,
    C = 3,
    B = 4,
    A = 5,
    S = 6
}

#endregion

public enum RollTier
{
    Fast, Mid, Fat, Overburdened
}