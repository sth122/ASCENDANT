using UnityEngine;

/// <summary>
/// 모든 Unit이 상속받는 애니메이션 제어 공통 베이스 컴포넌트
/// </summary>
public class UnitAnimController : MonoBehaviour, IAnimationHitBoxEvent, IAnimationActionEvent
{
    [field: SerializeField] protected Animator _animator;
    [field: SerializeField] protected AnimationDataSO _animData;

    public event System.Action OnHitboxOpenEvent;
    public event System.Action OnHitboxCloseEvent;
    public event System.Action OnActionCompletedEvent;

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
    /// 특정 레이어의 현재 애니메이션 재생 진행도(0.0 ~ 1.0)를 반환
    /// 트랜지션 중일 때는 다음 상태의 진행도를 함께 고려
    /// </summary>
    public float GetCurrentNormalizedTime(int layerIndex = 0)
    {
        if (_animator == null) return 0f;

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        return stateInfo.normalizedTime;
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

    public void SetTrigger(AnimParamType paramType)
    {
        if (_animData != null)
        {
            _animator.SetTrigger(_animData.GetParamHash(paramType));
        }
    }

    public void ResetTrigger(AnimParamType paramType)
    {
        if (_animData != null)
        {
            _animator.ResetTrigger(_animData.GetParamHash(paramType));
        }
    }

    public void SetInteger(AnimParamType paramType, int value)
    {
        if (_animData != null)
        {
            _animator.SetInteger(_animData.GetParamHash(paramType), value);
        }
    }

    public void SetFloat(AnimParamType paramType, float value)
    {
        if (_animData != null)
        {
            _animator.SetFloat(_animData.GetParamHash(paramType), value);
        }
    }

    public void SetBool(AnimParamType paramType, bool value)
    {
        if (_animData != null)
        {
            _animator.SetBool(_animData.GetParamHash(paramType), value);
        }
    }

    public void PlayState(AnimStateType stateType)
    {
        if (_animData != null && _animData.TryGetState(stateType, out int hash, out float duration))
        {
            _animator.CrossFadeInFixedTime(hash, duration);
        }
    }
    #endregion

    #region 공통 IAnimationHitBoxEvent, IAnimationActionEvent 구현부
    public void Anim_OnHitboxOpen() => OnHitboxOpenEvent?.Invoke();
    public void Anim_OnHitboxClose() => OnHitboxCloseEvent?.Invoke();
    public void Anim_OnActionCompleted() => OnActionCompletedEvent?.Invoke();
    #endregion
}
