using System.Collections.Generic;
using UnityEngine;
using Util = DebugLogger<BoossStatSO>;

[CreateAssetMenu(fileName = "BossStatSO", menuName = "Unit/BossStatData")]
public class BoossStatSO : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public UnitId Id;
        public BossGimmickStat Stat;
    }

    [SerializeField] private List<Entry> _bosses = new List<Entry>();
    private readonly Dictionary<UnitId, BossGimmickStat> _bossDictionary = new Dictionary<UnitId, BossGimmickStat>();

    public void InitializeDictionary()
    {
        _bossDictionary.Clear();
        foreach (Entry entry in _bosses)
        {
            if (!_bossDictionary.ContainsKey(entry.Id))
            {
                _bossDictionary.Add(entry.Id, entry.Stat);
            }
            else
            {
                Util.LogWarning($"중복된 UnitId 등록: {entry.Id}");
            }
        }
    }
    public bool TryGetPlayerStats(UnitId id, out BossGimmickStat stat)
    {
        return _bossDictionary.TryGetValue(id, out stat);
    }
}
