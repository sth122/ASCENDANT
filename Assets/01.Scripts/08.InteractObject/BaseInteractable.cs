/// <summary>
/// 모든 상호작용 가능 오브젝트의 최상위 추상 클래스
/// IInteractable 규약을 구현하며, InteractType에 따른 프롬프트 문자열을 자동 매핑합니다.
/// </summary>
public abstract class BaseInteractable : UnityEngine.MonoBehaviour, IInteractable
{
    [UnityEngine.Header("Interaction Configuration")]
    [UnityEngine.SerializeField] protected InteractType _interactType = InteractType.None;

    public virtual string InteractionPrompt => _interactType.GetPrompt();

    public abstract void OnInteract(PlayerController player);
}