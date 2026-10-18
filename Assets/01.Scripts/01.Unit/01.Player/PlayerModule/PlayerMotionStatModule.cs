using Util = DebugLogger<PlayerMotionStatModule>;

/// <summary>
/// Player 전용 기동력 및 장비 중량(Equip Load) 페널티를 전담 관리하는 순수 C# 모듈
/// </summary>
[System.Serializable]
public class PlayerMotionStatModule : UnitMotionStatModule
{
    #region Player 전용 기동 스탯
    public ModifiableStat SprintSpeed { get; }
    public ModifiableStat JumpForce { get; }
    public ModifiableStat RollForce { get; }
    #endregion

    #region 장비 중량 시스템
    public float CurrentEquipWeight { get; private set; }
    public float MaxEquipWeight { get; private set; } = 50.0f;
    public float WeightRatio => MaxEquipWeight > 0f ? CurrentEquipWeight / MaxEquipWeight : 0f;
    #endregion

    public PlayerMotionStatModule(PlayerStat playerStat) : base(playerStat)
    {
        if (playerStat == null)
        {
            Util.LogError("전달된 PlayerStat이 Null");
            return;
        }

        SprintSpeed = new ModifiableStat(playerStat.SprintSpeed);
        JumpForce = new ModifiableStat(playerStat.JumpForce);
        RollForce = new ModifiableStat(playerStat.RollForce);
        MaxEquipWeight = playerStat.CalculateMaxEquipWeight();
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

        InvokeMotionStatChanged();
    }

    /// <summary>
    /// 인스펙터 튜닝 시 ModifiableStat의 기본값을 실시간 동기화
    /// </summary>
    public void SyncBaseValues(PlayerStat playerStat)
    {
        if (playerStat == null) return;

        WalkSpeed.BaseValue = playerStat.WalkSpeed;
        SprintSpeed.BaseValue = playerStat.SprintSpeed;
        JumpForce.BaseValue = playerStat.JumpForce;
        RollForce.BaseValue = playerStat.RollForce;
        MaxEquipWeight = playerStat.CalculateMaxEquipWeight();

        UpdateWeight(CurrentEquipWeight);
    }
}
