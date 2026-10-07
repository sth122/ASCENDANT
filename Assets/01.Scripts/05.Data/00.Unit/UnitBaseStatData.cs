using System.Collections.Generic;
using UnityEngine;
using Util = DebugLogger<UnitStatSO>;

[CreateAssetMenu(fileName = "NewUnitStatData", menuName = "Unit/StatData")]
public class UnitStatSO : ScriptableObject
{
    [System.Serializable]
    public struct Unit
    {
        public UnitId Id;
        public UnitBaseStat Stats;
    }

    [SerializeField] private List<Unit> _unitList = new List<Unit>();
    private readonly Dictionary<UnitId, UnitBaseStat> _unitStatDictionary = new Dictionary<UnitId, UnitBaseStat>();

    public void InitializeDictionary()
    {
        _unitStatDictionary.Clear();
        foreach(Unit unit in _unitList)
        {
            if (!_unitStatDictionary.ContainsKey(unit.Id))
            {
                _unitStatDictionary.Add(unit.Id, unit.Stats);
            }
            else
            {
                Util.LogWarning($"중복된 UnitId 등록: {unit.Id}");
            }
        }
    }

    public bool TryGetUnitStat(UnitId unitId, out UnitBaseStat unitStat)
    {
        return _unitStatDictionary.TryGetValue(unitId, out unitStat);
    }
}
