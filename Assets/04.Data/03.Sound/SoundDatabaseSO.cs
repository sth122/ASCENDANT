using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// SoundId별 AudioClip, 볼륨, 피치 랜덤 범위, 카테고리를 관리하는 기획 SO
/// (AudioClip이 비어있어도 Null-Safe하게 동작하므로 파일 준비 전부터 코드 연동 가능)
/// </summary>
[CreateAssetMenu(fileName = "SoundDatabaseSO", menuName = "System/SoundDatabaseSO")]
public class SoundDatabaseSO : ScriptableObject, IInitable
{
    [System.Serializable]
    public struct SoundEntry
    {
        public SoundId Id;
        public SoundCategory Category;
        public AudioClip Clip;
        [Range(0f, 1f)] public float DefaultVolume;
        [Range(0f, 0.3f)] public float PitchRandomRange;
        public bool Is3DSpatial;
    }

    [SerializeField] private List<SoundEntry> _entries = new List<SoundEntry>();
    private readonly Dictionary<SoundId, SoundEntry> _entryMap = new Dictionary<SoundId, SoundEntry>();

    public void Init()
    {
        _entryMap.Clear();
        for (int i = 0; i < _entries.Count; i++)
        {
            _entryMap[_entries[i].Id] = _entries[i];
        }
    }

    public bool TryGetEntry(SoundId id, out SoundEntry entry)
    {
        if (_entryMap.Count == 0 && _entries.Count > 0) Init();
        return _entryMap.TryGetValue(id, out entry);
    }
}