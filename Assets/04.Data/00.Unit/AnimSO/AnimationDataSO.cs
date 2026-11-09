using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AnimationDataSO", menuName = "Unit/AnimationDataSO")]
public class AnimationDataSO : ScriptableObject
{
    [System.Serializable]
    public struct ParamEntry
    {
        public AnimParamType ParamType;
    }

    [System.Serializable]
    public struct StateEntry
    {
        public AnimStateType StateType;
        public AnimationClip Clip;
        [Range(0.1f, 3.0f)] public float SpeedMultiplier;
        public float CrossFadeDuration;
    }

    [SerializeField] private List<ParamEntry> _parameters = new List<ParamEntry>();
    [SerializeField] private List<StateEntry> _states = new List<StateEntry>();


    private readonly Dictionary<AnimParamType, int> _paramHashMap = new Dictionary<AnimParamType, int>();
    private readonly Dictionary<AnimStateType, (int Hash, float CrossFadeDuration, float ClipDuration)> _stateHashMap = new Dictionary<AnimStateType, (int Hash, float CrossFadeDuration, float ClipDuration)>();
    private bool _isInitialized = false;

    public void Init()
    {
        _paramHashMap.Clear();

        foreach (var p in _parameters)
        {
            _paramHashMap[p.ParamType] = Animator.StringToHash(p.ParamType.ToString());
        }

        _stateHashMap.Clear();
        foreach (var s in _states)
        {
            int stateHash = Animator.StringToHash(s.StateType.ToString());
            float speed = s.SpeedMultiplier <= 0f ? 1.0f : s.SpeedMultiplier;
            float effectiveDuration = (s.Clip != null ? s.Clip.length : 1.0f) / speed;

            _stateHashMap[s.StateType] = (stateHash, s.CrossFadeDuration, effectiveDuration);
        }
        _isInitialized = true;
    }
    
    public int GetParamHash(AnimParamType paramType)
    {
        if (!_isInitialized) Init();
        
        return _paramHashMap.TryGetValue(paramType, out int hash) ? hash : 0;
    }

    public bool TryGetState(AnimStateType stateType, out int hash, out float duration)
    {
        if(!_isInitialized) Init();

        if(_stateHashMap.TryGetValue(stateType, out var data))
        {
            hash = data.Hash;
            duration = data.CrossFadeDuration;
            return true;
        }
        hash = 0;
        duration = 1f;
        return false;
    }
    public float GetClipDuration(AnimStateType stateType)
    {
        if (!_isInitialized) Init();

        if (_stateHashMap.TryGetValue(stateType, out var data))
        {
            return data.ClipDuration;
        }

        return 1.0f; // 미등록 시 기본 안전값
    }
}
