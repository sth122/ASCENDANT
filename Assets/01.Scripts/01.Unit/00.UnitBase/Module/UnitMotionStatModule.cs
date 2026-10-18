using Util = DebugLogger<UnitMotionStatModule>;

/// <summary>
/// 모든 Unit(Player, Enemy, Boss)이 공유하는 기본 이동력 및 속도 버프/디버프 연산 추상 모듈
/// </summary>
[System.Serializable]
public abstract class UnitMotionStatModule : UnitBaseModule
{
    #region 공통 이동 속도
    public ModifiableStat WalkSpeed { get; protected set; }
    #endregion

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
    }

    protected void InvokeMotionStatChanged()
    {
        OnMotionStatChanged?.Invoke();
    }
}
