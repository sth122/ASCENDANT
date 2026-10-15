#region Unit Enum
public enum UnitState
{
    Idle,
    Move,
    Sprint,
    Jump,
    Attack,
    Parry,
    Stun,
    Roll,
    Riposte,
    GetHit,
    Die,
}

public enum UnitId
{
    // 고유 0번은 Player
    Player,

    // 11번부터 각 Enemy의 고유 명사
    Enemy = 11,

    // 101번부터 BossEnemy의 고유 명사
    Dragon = 101,

    // 201번부터 NPC의 고유 명사
    NPC = 201
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
    Idle,
    Move,
    Stun
}
#endregion