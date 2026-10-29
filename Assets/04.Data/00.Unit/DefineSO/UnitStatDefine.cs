using UnityEngine;

/// <summary>
/// 모든 Unit(Player, Enemy, NPC)이 공유하는 스탯 데이터 클래스
/// 공통적인 스탯만 가지고 있고, 플레이어 및 특수 유닛에 종속적이 스탯은 하위 클래스에서 구현
/// </summary>
[System.Serializable]
public abstract class UnitBaseInfo
{
    [field: Header("Unit Base Common Statsus")]
    [field: SerializeField] public float MaxHp { get; protected set; }
    [field: SerializeField] public float Poise { get; protected set; }
    [field: SerializeField] public float WalkSpeed { get; protected set; }

    public abstract UnitBaseInfo Clone();
}

#region PlayerStat
/// <summary>
/// Player 전용 확장 스탯 클래스
/// Player 조작에 필요한 기동력 ( 달리기, 회전, 점프, 구르기 ) 및
/// 성장 스탯( 생명력, 지구력, 근력, 기량 )과 Soul 데이터를 관리
/// </summary>
[System.Serializable]
public class PlayerInfo : UnitBaseInfo
{
    [field: Header("Player Movement Physics")]
    [field: SerializeField] public float SprintSpeed { get; private set; }
    [field: SerializeField] public float RotationSpeed { get; private set; }
    [field: SerializeField] public float JumpForce { get; private set; }
    [field: SerializeField] public float RollForce { get; private set; }
    [field: SerializeField] public float MaxStamina { get; private set; }


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

    [field: SerializeField] public float BaseMaxHp { get; private set; } = 500f;
    [field: SerializeField] public float BaseMaxStamina { get; private set; } = 100f;

    public float CalculateMaxHp() => BaseMaxHp + (Vigor * 25f);
    public float CalculateMaxStamina() => BaseMaxStamina + (Endurance * 2f);
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
        if (amount <= 0 || CurrentSouls < amount) return;
        CurrentSouls -= amount;
    }

    /// <summary>
    /// 원본 SO 훼손을 막기 위한 복제 메서드
    /// </summary>
    public override UnitBaseInfo Clone()
    {
        return new PlayerInfo
        {
            // 부모 필드 복사
            MaxHp = this.MaxHp,
            Poise = this.Poise,
            WalkSpeed = this.WalkSpeed,

            BaseMaxHp = this.BaseMaxHp,
            BaseMaxStamina = this.BaseMaxStamina,

            // 플레이어 고유 필드 복사
            SprintSpeed = this.SprintSpeed,
            RotationSpeed = this.RotationSpeed,
            JumpForce = this.JumpForce,
            RollForce = this.RollForce,
            MaxStamina = this.MaxStamina,
            SoulLevel = this.SoulLevel,
            CurrentSouls = this.CurrentSouls,
            Vigor = this.Vigor,
            Endurance = this.Endurance,
            Strength = this.Strength,
            Dexterity = this.Dexterity
        };
    }

    /// <summary>
    /// 세이브 파일의 성장 수치(PlayerSaveData)를 주입받아 런타임 스탯을 재연산
    /// </summary>
    public void ApplySaveData(PlayerSaveData saveData)
    {
        if (saveData == null) return;

        SoulLevel = saveData.SoulLevel;
        CurrentSouls = saveData.CurrentSouls;
        Vigor = saveData.Vigor;
        Endurance = saveData.Endurance;
        Strength = saveData.Strength;
        Dexterity = saveData.Dexterity;

        MaxHp = CalculateMaxHp();
        MaxStamina = CalculateMaxStamina();
    }
}

#endregion

#region Boss
/// <summary>
/// UnitBaseStat을 상속받아 추후 구현하게 될 보스 전용 스탯 클래스
/// </summary>
[System.Serializable]
public class BossInfo : EnemyInfo
{
    [field: Header("Boss Phase & Gimmick Balance")]
    [field: Tooltip("2페이즈 전환 체력 비율")]
    [field: SerializeField] public float Phase2ThresholdRatio { get; private set; } = 0.30f;

    [field: Tooltip("2페이즈 전환 시 체력 즉시 회복 비율")]
    [field: SerializeField] public float Phase2HealRatio { get; private set; } = 0.10f;

    [field: Tooltip("2페이즈 공격력 및 애니메이션 속도 배율")]
    [field: SerializeField] public float Phase2StatBuffMultiplier { get; private set; } = 1.30f;

    [field: Tooltip("스킬 사이클 기본 발동 간격")]
    [field: SerializeField] public float SkillCycleInterval { get; private set; } = 15.0f;

    [field: Tooltip("2페이즈 스킬 즉시 쿨타임 초기화 확률")]
    [field: SerializeField] public float SkillCooldownResetChance { get; private set; } = 0.30f;

    public BossInfo()
    {
        Tier = EnemyTier.Boss;
    }

    /// <summary>
    /// 부모 필드(MaxHp, Poise 등) 유실을 원천 방지하는 완전한 깊은 복제(Deep Copy)
    /// </summary>
    public override UnitBaseInfo Clone()
    {
        return new BossInfo
        {
            // UnitBaseInfo 공통
            MaxHp = this.MaxHp,
            Poise = this.Poise,
            WalkSpeed = this.WalkSpeed,

            // EnemyInfo 계승
            Tier = EnemyTier.Boss,
            DropSouls = this.DropSouls,
            AttackPower = this.AttackPower,
            BasePoiseDamage = this.BasePoiseDamage,
            AttackRange = this.AttackRange,
            AttackCooldown = this.AttackCooldown,
            ChaseSpeed = this.ChaseSpeed,
            PatrolSpeed = this.PatrolSpeed,
            MaxLeashDistance = this.MaxLeashDistance,
            SightRadius = this.SightRadius,
            SightAngle = this.SightAngle,
            HearingRadius = this.HearingRadius,

            // BossInfo 고유 기믹
            Phase2ThresholdRatio = this.Phase2ThresholdRatio,
            Phase2HealRatio = this.Phase2HealRatio,
            Phase2StatBuffMultiplier = this.Phase2StatBuffMultiplier,
            SkillCycleInterval = this.SkillCycleInterval,
            SkillCooldownResetChance = this.SkillCooldownResetChance
        };
    }
}
#endregion

#region EnemyStat

/// <summary>
/// 일반 Enemy 전용 확장 기본 스탯 데이터 클래스
/// </summary>
[System.Serializable]
public class EnemyInfo : UnitBaseInfo
{
    #region 등급 및 처치 보상
    [field: Header("Classification & Reward")]
    [field: SerializeField] public EnemyTier Tier { get; protected set; } = EnemyTier.Normal;
    [field: SerializeField] public int DropSouls { get; protected set; }
    #endregion

    #region 전투 스탯 (Combat)
    [field: Header("Enemy Combat Status")]
    [field: SerializeField] public float AttackPower { get; protected set; }
    [field: SerializeField] public float BasePoiseDamage { get; protected set; }
    [field: SerializeField] public float AttackRange { get; protected set; }
    [field: SerializeField] public float AttackCooldown { get; protected set; }
    #endregion

    #region 기동 및 활동 영역 스탯 (Locomotion & Leash)
    [field: Header("Enemy Locomotion & Leash")]
    [field: SerializeField] public float ChaseSpeed { get; protected set; }
    [field: SerializeField] public float PatrolSpeed { get; protected set; }
    [field: Tooltip("최대 추적 범위")]
    [field: SerializeField] public float MaxLeashDistance { get; protected set; }
    #endregion

    #region 지각 센서 스탯 (Perception Sensors)
    [field: Header("Enemy Perception Sensor")]
    [field: SerializeField] public float SightRadius { get; protected set; }
    [field: Range(0f, 360f)]
    [field: SerializeField] public float SightAngle { get; protected set; }
    [field: SerializeField] public float HearingRadius { get; protected set; }
    #endregion

    /// <summary>
    /// 원본 ScriptableObject 데이터 보호를 위한 깊은 복사(Deep Copy) 메서드
    /// 런타임 상태에서 버프/디버프 등에 의해 스탯이 변조되어도 원본 에셋이 유지되도록 처리
    /// </summary>
    public override UnitBaseInfo Clone()
    {
        return new EnemyInfo
        {
            // 1. UnitBaseStat 공통 수치 복제
            MaxHp = this.MaxHp,
            Poise = this.Poise,
            WalkSpeed = this.WalkSpeed,

            // 2. Enemy 등급 및 처치 보상 수치 복제
            Tier = this.Tier,
            DropSouls = this.DropSouls,

            // 3. Enemy 전투 수치 복제
            AttackPower = this.AttackPower,
            BasePoiseDamage = this.BasePoiseDamage,
            AttackRange = this.AttackRange,
            AttackCooldown = this.AttackCooldown,

            // 4. Enemy 기동 및 리쉬 수치 복제
            ChaseSpeed = this.ChaseSpeed,
            PatrolSpeed = this.PatrolSpeed,
            MaxLeashDistance = this.MaxLeashDistance,

            // 5. Enemy 지각 센서 수치 복제
            SightRadius = this.SightRadius,
            SightAngle = this.SightAngle,
            HearingRadius = this.HearingRadius
        };
    }
}
#endregion