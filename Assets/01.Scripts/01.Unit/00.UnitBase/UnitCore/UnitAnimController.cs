using UnityEngine;

/// <summary>
/// 모든 Unit이 상속받는 애니메이션 제어 공통 베이스 컴포넌트
/// </summary>
public class UnitAnimController : MonoBehaviour
{
    [field: SerializeField] protected Animator _animator;
    [field: SerializeField] protected AnimationDataSO _animData;
    private AnimStateType _currentStateType = (AnimStateType)(-1);
    private int _currentStateHash = 0;


    private readonly System.Collections.Generic.Dictionary<int, float> _clipDurationMap = new System.Collections.Generic.Dictionary<int, float>();

    public Animator AnimatorInstance => _animator;
    public AnimationDataSO AnimData => _animData;
    public AnimStateType CurrentStateType => _currentStateType;
    public int CurrentStateHash => _currentStateHash;

    protected virtual void Awake()
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }
        if (_animator != null)
        {
            _animData.Init();
        }
        // 일반 로코모션 상태에서는 스크립트 물리 이동을 기본으로 사용
        SetRootMotion(false);
        CacheClipDurations();
    }

    #region Animator 공통 사용 구현부
    /// <summary>
    /// 애니메이터의 Root Motion 활성화/비활성화 전환
    /// 애니메이션 궤적을 물리 엔진에 적용
    /// </summary>
    public void SetRootMotion(bool isEnabled)
    {
        if(_animator != null)
        {
            _animator.applyRootMotion = isEnabled;
        }
    }
    /// <summary>
    /// 현재 재생 중인 애니메이션의 normalizedTime 반환
    /// </summary>
    public float GetCurrentNormalizedTime(int layerIndex = 0)
    {
        if (_animator == null) return 0f;

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        return stateInfo.normalizedTime;
    }

    public void SetTrigger(AnimParamType paramType)
    {
        if (_animData != null && _animator != null)
        {
            _animator.SetTrigger(_animData.GetParamHash(paramType));
        }
    }

    public void ResetTrigger(AnimParamType paramType)
    {
        if (_animData != null && _animator != null)
        {
            _animator.ResetTrigger(_animData.GetParamHash(paramType));
        }
    }

    public void SetInteger(AnimParamType paramType, int value)
    {
        if (_animData != null && _animator != null)
        {
            _animator.SetInteger(_animData.GetParamHash(paramType), value);
        }
    }

    public void SetFloat(AnimParamType paramType, float value)
    {
        if (_animData != null && _animator != null)
        {
            _animator.SetFloat(_animData.GetParamHash(paramType), value);
        }
    }

    public void SetBool(AnimParamType paramType, bool value)
    {
        if (_animData != null && _animator != null)
        {
            _animator.SetBool(_animData.GetParamHash(paramType), value);
        }
    }

    public void PlayState(AnimStateType stateType, bool force = false)
    {
        if (_animData == null || _animator == null) return;
        if (!force && _currentStateType == stateType) return;

        if (_animData.TryGetState(stateType, out int hash, out float duration))
        {
            _currentStateType = stateType;
            _currentStateHash = hash;

            // 베이스 레이어(0)에 해당 상태 해시가 실제로 존재하는지 검증 후 재생
            if (_animator.HasState(0, hash))
            {
                _animator.CrossFadeInFixedTime(hash, duration, 0);
            }
        }
    }
    /// <summary>
    /// 외부에서 강제로 상태 캐시를 무효화해야 할 때 호출 (상태 강제 리셋용)
    /// </summary>
    public void ResetStateCache()
    {
        _currentStateType = (AnimStateType)(-1);
        _currentStateHash = 0;
    }

    /// <summary>
    /// RuntimeAnimatorController에 바인딩된 모든 애니메이션 클립의 길이를 사전 캐싱
    /// </summary>
    private void CacheClipDurations()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;

        _clipDurationMap.Clear();
        AnimationClip[] clips = _animator.runtimeAnimatorController.animationClips;

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null)
            {
                int clipHash = Animator.StringToHash(clip.name);
                _clipDurationMap[clipHash] = clip.length;
            }
        }
    }

    /// <summary>
    /// 지정된 애니메이션 상태(State)의 실제 재생 길이(초 단위)를 반환
    /// 상태명과 일치하는 클립 길이를 우선 조회하며, 없을 경우 기본 1.0초를 안전 반환합니다.
    /// </summary>
    public float GetStateDuration(AnimStateType stateType)
    {
        int stateHash = Animator.StringToHash(stateType.ToString());

        if (_clipDurationMap.TryGetValue(stateHash, out float duration))
        {
            return duration;
        }

        // AnimatorStateInfo 기반 런타임 폴백 조회
        if (_animator != null)
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash == stateHash && stateInfo.length > 0f)
            {
                return stateInfo.length;
            }
        }

        return 1.0f; // 기본 안전값
    }
    #endregion
}
