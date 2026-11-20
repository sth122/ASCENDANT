/// <summary>
/// 아이템 사용 시 발동되는 효과의 추상 기반 ScriptableObject
/// </summary>
public abstract class ItemEffectSO : UnityEngine.ScriptableObject
{
    /// <summary>
    /// 아이템 효과 실행
    /// </summary>
    /// <param name="user">아이템을 사용한 주체 게임오브젝트</param>
    public abstract bool Execute(UnityEngine.GameObject user);
}