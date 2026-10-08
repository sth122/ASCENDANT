using System;
using Util = DebugLogger<UnitMotionStatModule>;

/// <summary>
/// 유닛의 이동 수치를 보관하고, 장비 무게(Equip Load)에 따른 패널티를 더티 플래그 패턴으로 관리하는 순수 C# 모듈
/// 전달받은 스탯 타입(UnitBaseStat / PlayerStat)에 맞춰 모듈 수치를 적응형으로 설정
/// </summary>
[Serializable]
public class UnitMotionStatModule
{
    public ModifiableStat WalkSpeed { get; }
    public ModifiableStat SprintSpeed { get; }
    public ModifiableStat JumpForce { get; }
    public ModifiableStat RollForce { get; }

    public float CurrentEquipWeight { get; private set; }
    public float MaxEquipWeight { get; private set; } = 50.0f;

    public float WeightRatio => MaxEquipWeight > 0f ? CurrentEquipWeight / MaxEquipWeight : 0f;

    public event System.Action OnMotionStatChanged;
    public UnitMotionStatModule(UnitBaseStat baseStat)
    {
        if (baseStat == null)
        {
            Util.LogError($"{baseStat} Null 레퍼런스");
            return;
        }

        // 공통 스탯 
        WalkSpeed = new ModifiableStat(baseStat.WalkSpeed);

        // 플레이어 전용 스탯 분기 처리 -> 추후 보스 스탯 분기 처리도 만들어야 함
        if(baseStat is PlayerStat playerStat)
        {
            SprintSpeed = new ModifiableStat(playerStat.SprintSpeed);
            JumpForce = new ModifiableStat(playerStat.JumpForce);
            RollForce = new ModifiableStat(playerStat.RollForce);
            MaxEquipWeight = playerStat.CalculateMaxEquipWeight();
        }
        else
        {
            SprintSpeed = new ModifiableStat(0f);
            JumpForce = new ModifiableStat(0f);
            RollForce = new ModifiableStat(0f);
        }


    }

    /// <summary>
    /// 장비 무게가 변경되었을 때만 호출하는 메서드
    /// 무게 상태에 따라 1회만 Modifier를 재구성
    /// 실제 수치 계산은 이후 Value가 처음 읽힐 때 단 1회 연산 후 캐싱
    /// </summary>
    public void UpdateWeight(float totalWeight)
    {
        CurrentEquipWeight = totalWeight;

        RollForce.ClearModifiers();
        WalkSpeed.ClearModifiers();
        SprintSpeed.ClearModifiers();

        RollForce.ClearModifiers();
        if (WeightRatio >= 0.7f)
        {
            RollForce.AddModifier(new StatModifier(-0.3f, StatModType.PercentMult));     // 구르기 거리 30% 감소
            WalkSpeed.AddModifier(new StatModifier(-0.2f, StatModType.PercentMult));     // 걷기 속도 20% 감소
            SprintSpeed.AddModifier(new StatModifier(-0.25f, StatModType.PercentMult));  // 달리기 속도 25% 감소
        }

        OnMotionStatChanged?.Invoke();
    }
}
