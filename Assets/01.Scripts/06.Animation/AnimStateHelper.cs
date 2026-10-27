/// <summary>
/// AnimStateType 범위 기반 추첨 및 보조 기능을 제공하는 정적 헬퍼 클래스
/// </summary>
public static class AnimStateHelper
{
    public const AnimStateType HitMin = AnimStateType.Hit1;
    public const AnimStateType HitMax = AnimStateType.Hit2;

    public const AnimStateType DeathMin = AnimStateType.Death1;
    public const AnimStateType DeathMax = AnimStateType.Death2;

    /// <summary>
    /// 등록된 Hit 모션 범위 내에서 무작위 상태를 1종 선택하여 반환
    /// </summary>
    public static AnimStateType GetRandomHitState()
    {
        int randomIndex = UnityEngine.Random.Range((int)HitMin, (int)HitMax + 1);
        return (AnimStateType)randomIndex;
    }

    /// <summary>
    /// 등록된 Death 모션 범위 내에서 무작위 상태를 1종 선택하여 반환
    /// </summary>
    public static AnimStateType GetRandomDeathState()
    {
        int randomIndex = UnityEngine.Random.Range((int)DeathMin, (int)DeathMax + 1);
        return (AnimStateType)randomIndex;
    }
}