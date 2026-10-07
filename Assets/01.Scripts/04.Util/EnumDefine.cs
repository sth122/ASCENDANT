#region unitEnum
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
    Die
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