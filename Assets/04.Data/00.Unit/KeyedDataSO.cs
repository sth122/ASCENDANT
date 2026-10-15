using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 중복되던 PlayerStatSO, BossStatSO, UnitStatSO를 단일 제네릭 구조로 추상화한 데이터 베이스 SO
/// </summary>
public abstract class KeyedDataSO<TKey, TValue> : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public TKey Id;
        public TValue Value;
    }

    [SerializeField] private List<Entry> _entries = new List<Entry>();
    private readonly Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();
    private bool _isInitialized = false;

    public virtual void InitializeDictionary()
    {
        _dictionary.Clear();
        for (int i = 0; i < _entries.Count; i++)
        {
            TKey key = _entries[i].Id;
            if (!_dictionary.ContainsKey(key))
            {
                _dictionary.Add(key, _entries[i].Value);
            }
            else
            {
                DebugLogger.LogWarningWithTag(name, $"중복된 키 등록 시도: {key}");
            }
        }
        _isInitialized = true;
    }

    public bool TryGetData(TKey key, out TValue result)
    {
        if (!_isInitialized)
        {
            InitializeDictionary();
        }
        return _dictionary.TryGetValue(key, out result);
    }

    private void OnValidate()
    {
        _isInitialized = false;
    }
}