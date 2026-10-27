using UnityEngine;

/// <summary>
/// 모든 Unit이 상속받는 애니메이션 제어 공통 베이스 컴포넌트
/// </summary>
public class UnitAnimController : MonoBehaviour
{
    [field: SerializeField] protected Animator _animator;
    [field: SerializeField] protected AnimationDataSO _animData;

    public Animator AnimatorInstance => _animator;
    public AnimationDataSO AnimData => _animData;

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

    /// <summary>
    /// 목표 상태(targetStateHash)의 진행도를 안전하게 추출하는 메서드
    /// 트랜지션 중에는 다음 상태(NextState)의 진행도를 읽고, 
    /// 목표 상태가 아니면 0f를 반환하여 이전 루프 모션 시간(예: BattleIdle 3.5) 오독을 원천 방지
    /// </summary>
    public float GetCurrentNormalizedTimeOfState(int targetStateHash, int layerIndex = 0)
    {
        if (_animator == null) return 0f;

        // 1. 현재 트랜지션 중인 경우
        if (_animator.IsInTransition(layerIndex))
        {
            AnimatorStateInfo nextInfo = _animator.GetNextAnimatorStateInfo(layerIndex);
            if (nextInfo.shortNameHash == targetStateHash)
            {
                return nextInfo.normalizedTime;
            }
            return 0f; // 아직 전환 대상이 아니면 0 반환
        }

        // 2. 트랜지션이 완료된 현재 상태인 경우
        AnimatorStateInfo currentInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (currentInfo.shortNameHash == targetStateHash)
        {
            return currentInfo.normalizedTime;
        }

        return 0f;
    }

    /// <summary>
    /// 현재 클립이 완전히 1회 종료되었는지 검사 (트랜지션 중이 아닐 때)
    /// </summary>
    public bool IsAnimationFinished(int layerIndex = 0)
    {
        if (_animator == null) return true;

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        return stateInfo.normalizedTime >= 1.0f && !_animator.IsInTransition(layerIndex);
    }

    public bool IsInTransition(int layerIndex = 0) => _animator != null && _animator.IsInTransition(layerIndex);

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

    public int GetStateHash(AnimStateType stateType)
    {
        if (_animData != null && _animData.TryGetState(stateType, out int hash, out _))
        {
            return hash;
        }
        return 0;
    }

    public void PlayState(AnimStateType stateType)
    {
        if (_animData == null || _animator == null) return;

        if (_animData.TryGetState(stateType, out int hash, out float duration))
        {
            // 베이스 레이어(0)에 해당 상태 해시가 실제로 존재하는지 검증 후 재생
            if (_animator.HasState(0, hash))
            {
                _animator.CrossFadeInFixedTime(hash, duration, 0);
            }
        }
    }
    #endregion
}
