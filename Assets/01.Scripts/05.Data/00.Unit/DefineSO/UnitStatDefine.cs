using UnityEngine;

/// <summary>
/// Unit(Player, Enemy, NPC)의 기반이 되는 최상위 스탯 클래스
/// </summary>
[System.Serializable]
public class UnitBaseStat
{
    [Header("Life & defense")]
    [field: SerializeField] public float MaxHp { get; protected set; }
    [field: SerializeField] public float Poise { get; protected set; }

    [Header("Drop Rewards")]
    [field: SerializeField] public int DropSouls { get; protected set; }

    [Header("Movement Physics")]
    [field: SerializeField] public float WalkSpeed { get; protected set; }
    [field: SerializeField] public float SprintSpeed { get; protected set; }
    [field: SerializeField] public float RotationSpeed { get; protected set; }
    [field: SerializeField] public float JumpForce { get; protected set; }
    [field: SerializeField] public float RollForce { get; protected set; }

    /// <summary>
    /// 원본 SO 훼손을 막기 위한 복제 메서드
    /// </summary>
    public virtual UnitBaseStat Clone()
    {
        return new UnitBaseStat
        {
            MaxHp = this.MaxHp,
            Poise = this.Poise,
            DropSouls = this.DropSouls,
            WalkSpeed = this.WalkSpeed,
            SprintSpeed = this.SprintSpeed,
            RotationSpeed = this.RotationSpeed,
            JumpForce = this.JumpForce,
            RollForce = this.RollForce
        };
    }
}


/// <summary>
/// UnitBaseStat을 상속받아 플레이어 레벨업 및 능력치를 추가한 Player 스탯 클래스
/// </summary>
[System.Serializable]
public class PlayerStat : UnitBaseStat
{
    [field: Header("Level & Souls")]
    [field: SerializeField] public int SoulLevel { get; private set; }
    [field: SerializeField] public int CurrentSouls { get; private set; }

    [field: Header("Core Attributes")]
    [field: Tooltip("생명력 (Vigor): 최대 HP 산출 기준")]
    [field: SerializeField] public int Vigor { get; private set; }

    [field: Tooltip("지구력 (Endurance): 최대 스태미나 및 중량 산출 기준")]
    [field: SerializeField] public int Endurance { get; private set; }

    [field: Tooltip("근력 (Strength): 무기 장착치 및 공격력")]
    [field: SerializeField] public int Strength { get; private set; }

    [field: Tooltip("기량 (Dexterity): 무기 장착치 및 공격 속도")]
    [field: SerializeField] public int Dexterity { get; private set; }

    // 스탯 수치 기반 유도 계산식
    public float CalculateMaxHp() => 400f + (Vigor * 25f);
    public float CalculateMaxStamina() => 80f + (Endurance * 2f);
    public float CalculateMaxEquipWeight() => 40f + (Endurance * 1.5f);

    /// <summary>
    /// 적 처치 또는 소울 아이템 사용 시 소울 획득
    /// </summary>
    public void AddSouls(int amount)
    {
        if (amount <= 0)
            return;
        CurrentSouls += amount;
    }

    /// <summary>
    /// 레벨업 또는 상점 구매 시 소울 소모
    /// </summary>
    public void ConsumeSouls(int amount)
    {
        if (amount <= 0 || CurrentSouls < amount)
            return;
        CurrentSouls -= amount;
    }

    /// <summary>
    /// 원본 SO 훼손을 막기 위한 복제 메서드
    /// </summary>
    public override UnitBaseStat Clone()
    {
        return new PlayerStat
        {
            // 부모 필드 복사
            MaxHp = this.MaxHp,
            Poise = this.Poise,
            DropSouls = this.DropSouls,
            WalkSpeed = this.WalkSpeed,
            SprintSpeed = this.SprintSpeed,
            RotationSpeed = this.RotationSpeed,
            JumpForce = this.JumpForce,
            RollForce = this.RollForce,

            // 플레이어 고유 필드 복사
            SoulLevel = this.SoulLevel,
            CurrentSouls = this.CurrentSouls,
            Vigor = this.Vigor,
            Endurance = this.Endurance,
            Strength = this.Strength,
            Dexterity = this.Dexterity
        };
    }
}

/// <summary>
/// UnitBaseStat을 상속받아 추후 구현하게 될 보스 전용 스탯 클래스
/// </summary>
[System.Serializable]
public class BossGimmickStat : UnitBaseStat
{

}