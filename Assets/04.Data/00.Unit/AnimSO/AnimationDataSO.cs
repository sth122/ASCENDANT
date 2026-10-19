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
        public float CrossFadeDuration;
    }

    [SerializeField] private List<ParamEntry> _parameters = new List<ParamEntry>();
    [SerializeField] private List<StateEntry> _states = new List<StateEntry>();


    private readonly Dictionary<AnimParamType, int> _paramHashMap = new Dictionary<AnimParamType, int>();
    private readonly Dictionary<AnimStateType, (int Hash, float Duration)> _stateHashMap = new Dictionary<AnimStateType, (int Hash, float Duration)>();
    private bool _isInitialized = false;

    public void Init()
    {
        _paramHashMap.Clear();

        foreach (var p in _parameters)
        {
            _paramHashMap[p.ParamType] = Animator.StringToHash(p.ParamType.ToString());
        }

        _stateHashMap.Clear();
        foreach(var s in _states)
        {
            _stateHashMap[s.StateType] = (Animator.StringToHash(s.StateType.ToString()), s.CrossFadeDuration);
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
            duration = data.Duration;
            return true;
        }
        hash = 0;
        duration = 0.1f;
        return false;
    }

}
