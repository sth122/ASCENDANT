public class BonfireModel : IUIModel
{
    public int BaseLevel;
    public int TargetLevel;
    public int CurrentSouls;
    public int RequiredSouls;

    public int AddVig, AddEnd, AddStr, AddDex;

    public void Reset()
    {
        AddVig = AddEnd = AddStr = AddDex = 0;
    }
}