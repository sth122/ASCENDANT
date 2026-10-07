using UnityEngine;
using Util = DebugLogger<UnitStatController>;

/// <summary>
/// 각 Unit ( Player, Enemy 등 ) 오브젝트에 부착하여 해당 객체의 실시간 스탯을 관리하는 컴포넌트
/// </summary>
public class UnitStatController : MonoBehaviour
{
    public UnitMotionStatModule MotionStats { get; private set; }
    #region 런타임 가변 수치
    public float CurrentHp { get; private set; }
    public float MaxHp { get; private set; }
    public float CurrentPoise { get; private set; }
    public int DropSouls { get; private set; }
    #endregion

    public event System.Action OnDieEvent;
    public event System.Action<float> OnHpChanged;

    public void Init(UnitBaseStat baseStat)
    {
        if(baseStat == null)
        {
            Util.LogError($"{gameObject.name} 전달된 UnitBaseStat이 Null");
            return;
        }

        MaxHp = baseStat.MaxHp;
        CurrentHp = MaxHp;
        CurrentPoise = baseStat.Poise;
        DropSouls = baseStat.DropSouls;
        MotionStats = new UnitMotionStatModule(baseStat);
    }


}
