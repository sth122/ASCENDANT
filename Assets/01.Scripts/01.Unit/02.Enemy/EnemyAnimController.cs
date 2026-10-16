/// <summary>
/// 모든 Enemy가 공유하는 Animation Controller
/// </summary>
public class EnemyAnimController : UnitAnimController
{
    public void  PlayAttackAnimation(string animStateName, float crossFadeDuration = 0.15f)
    {
        PlayCustomState(animStateName, crossFadeDuration);
    }

    public void PlayCustomState(string stateName, float crossFadeDuration = 0.1f)
    {
        if (_animator != null)
        {
            _animator.CrossFadeInFixedTime(UnityEngine.Animator.StringToHash(stateName), crossFadeDuration);
        }
    }
}
