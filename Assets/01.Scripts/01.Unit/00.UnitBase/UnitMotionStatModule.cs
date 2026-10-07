using Util = DebugLogger<UnitMotionStatModule>;

/// <summary>
/// PlayerMovement와 FSM이 직접 읽어갈 이동 관련 수치를 모아둔 순수 C# 모듈
/// </summary>
public class UnitMotionStatModule
{
    public ModifiableStat WalkSpeed { get; }
    public ModifiableStat SprintSpeed { get; }
    public ModifiableStat JumpForce { get; }
    public ModifiableStat RollForce { get; }

    public float CurrentEquipWeight { get; private set; }
    public float MaxEquipWeight { get; private set; } = 50.0f;

    public event System.Action OnMotionStatChanged;
    public UnitMotionStatModule(UnitBaseStat baseStat)
    {
        if(baseStat == null)
        {
            Util.LogError($"{baseStat} Null 레퍼런스");
            return;
        }

        float walk = baseStat.WalkSpeed;
        float sprint = baseStat.SprintSpeed;
        float jump = baseStat.JumpForce;
        float roll = baseStat.RollForce;

        WalkSpeed = new ModifiableStat(walk);
        SprintSpeed = new ModifiableStat(sprint);
        JumpForce = new ModifiableStat(jump);
        RollForce = new ModifiableStat(roll);
    }

    public void UpdateWeight(float totalWeight)
    {
        CurrentEquipWeight = totalWeight;
        float ratio = CurrentEquipWeight / MaxEquipWeight;

        RollForce.ClearModifiers();
        if(ratio >= 0.7f)
        {
            RollForce.AddModifier(new StatModifier(-0.3f, StatModType.PercentMult));
        }

        OnMotionStatChanged?.Invoke();
    }
}
