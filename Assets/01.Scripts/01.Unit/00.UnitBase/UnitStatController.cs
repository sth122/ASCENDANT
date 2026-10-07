using UnityEngine;
using Util = DebugLogger<UnitStatController>;

public class UnitStatController : MonoBehaviour
{
    public UnitMotionStatModule MotionStats { get; private set; }
    public int DropSouls { get; private set; }

    public void Init(UnitBaseStat baseStat)
    {
        if(baseStat == null)
        {
            Util.LogError($"{gameObject.name} 전달된 UnitBaseStat이 Null");
            return;
        }

        DropSouls = baseStat.DropSouls;
        MotionStats = new UnitMotionStatModule(baseStat);
    }
}
