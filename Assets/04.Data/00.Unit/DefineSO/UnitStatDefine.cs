using UnityEngine;

/// <summary>
/// 모든 Unit(Player, Enemy, NPC)이 공유하는 스탯 데이터 클래스
/// 공통적인 스탯만 가지고 있고, 플레이어 및 특수 유닛에 종속적이 스탯은 하위 클래스에서 구현
/// </summary>
[System.Serializable]
public class UnitBaseStat
{
    [Header("Life & defense")]
    [field: SerializeField] public float MaxHp { get; protected set; }
    [field: SerializeField] public float Poise { get; protected set; }

    [Header("Drop Rewards")]
    [field: SerializeField] public int DropSouls { get; protected set; }

    [Header("Basic Movement Physics")]
    [field: SerializeField] public float WalkSpeed { get; protected set; }

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
        };
    }
}


/// <summary>
/// Player 전용 확장 스탯 클래스
/// Player 조작에 필요한 기동력 ( 달리기, 회전, 점프, 구르기 ) 및
/// 성장 스탯( 생명력, 지구력, 근력, 기량 )과 Soul 데이터를 관리
/// </summary>
[System.Serializable]
public class PlayerStat : UnitBaseStat
{
    [field: Header("Player Movement Physics")]
    [field: SerializeField] public float SprintSpeed { get; protected set; }
    [field: SerializeField] public float RotationSpeed { get; protected set; }
    [field: SerializeField] public float JumpForce { get; protected set; }
    [field: SerializeField] public float RollForce { get; protected set; }

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
        if (amount <= 0) return;
        CurrentSouls += amount;
    }

    /// <summary>
    /// 레벨업 또는 상점 구매 시 소울 소모
    /// </summary>
    public void ConsumeSouls(int amount)
    {
        if (amount <= 0 || CurrentSouls < amount)  return;
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

            // 플레이어 고유 필드 복사
            SprintSpeed = this.SprintSpeed,
            RotationSpeed = this.RotationSpeed,
            JumpForce = this.JumpForce,
            RollForce = this.RollForce,
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