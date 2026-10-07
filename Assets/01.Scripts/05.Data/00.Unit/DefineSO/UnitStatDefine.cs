using UnityEngine;

/// <summary>
/// Player, Enemy, NPC 모두가 공통으로 가지는 기본 스탯 클래스
/// </summary>
[System.Serializable]
public class UnitBaseStat
{
    [Header("Life & defense")]
    [field: SerializeField] public float MaxHp { get; private set; }
    [field: SerializeField] public float Poise { get; private set; }

    [Header("Movement Physics")]
    [field: SerializeField] public float WalkSpeed { get; private set; }
    [field: SerializeField] public float SprintSpeed { get; private set; }
    [field: SerializeField] public float RotationSpeed { get; private set; }
    [field: SerializeField] public float JumpForce { get; private set; }
    [field: SerializeField] public float RollForce { get; private set; }
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
}

/// <summary>
/// UnitBaseStat을 상속받아 추후 구현하게 될 보스 전용 스탯 클래스
/// </summary>
[System.Serializable]
public class BossGimmickStat : UnitBaseStat
{

}