using System.Collections.Generic;
using UnityEngine;
using Util = DebugLogger<PlayerStatSO>;

[CreateAssetMenu(fileName = "PlayerStatSO", menuName = "Unit/PlayerStatData")]

public class PlayerStatSO : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public UnitId Id;
        public PlayerStat Stat;
    }

    [SerializeField] private List<Entry> _players = new List<Entry>();
    private readonly Dictionary<UnitId, PlayerStat> _playerDictionary = new Dictionary<UnitId, PlayerStat>();

    public void InitializeDictionary()
    {
        _playerDictionary.Clear();
        foreach (Entry entry in _players)
        {
            if (!_playerDictionary.ContainsKey(entry.Id))
            {
                _playerDictionary.Add(entry.Id, entry.Stat);
            }
            else
            {
                Util.LogWarning($"중복된 UnitId 등록: {entry.Id}");
            }
        }
    }
    public bool TryGetPlayerStats(UnitId id, out PlayerStat stat)
    {
        return _playerDictionary.TryGetValue(id, out stat);
    }
}
