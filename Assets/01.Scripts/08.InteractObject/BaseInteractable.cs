using System.Collections.Generic;

/// <summary>
/// 모든 상호작용 가능 오브젝트의 최상위 추상 클래스
/// IInteractable 규약을 구현하며, InteractType에 따른 프롬프트 문자열을 자동 매핑합니다.
/// </summary>
public abstract class BaseInteractable : UnityEngine.MonoBehaviour, IInteractable
{
    [UnityEngine.Header("Interaction Configuration")]
    [UnityEngine.SerializeField] protected InteractType _interactType = InteractType.None;

    public virtual string InteractionPrompt => _interactType.GetPrompt();

    /// <summary>
    /// true인 경우 OnInteract 호출 시 PlayerHUDView의 다중 선택지(InteractionPromptUI)를 엽니다.
    /// </summary>
    protected virtual bool HasMultipleChoices => false;

    /// <summary>
    /// 다중 선택지 목록 구성 (HasMultipleChoices == true일 때 하위 클래스에서 구현)
    /// </summary>
    protected virtual void BuildInteractionChoices(PlayerController player, List<InteractionChoice> outChoices) { }

    public virtual void OnInteract(PlayerController player)
    {
        if (player == null || !HasMultipleChoices) return;

        if (UIManager.Instance != null &&
            UIManager.Instance.TryGetActiveView<PlayerHUDView>(UIId.PlayerHUD, out PlayerHUDView hudView) &&
            hudView.InteractionPrompt != null)
        {
            if (hudView.InteractionPrompt.IsChoiceMenuOpen) return;

            List<InteractionChoice> choices = new List<InteractionChoice>(4);
            BuildInteractionChoices(player, choices);
            if (choices.Count == 0) return;

            hudView.OpenInteractionChoices(choices, onClosed: () =>
            {
                // 선택지에서 '레벨 업' 등 새 UI 창을 연 경우에는 힌트를 다시 띄우지 않음
                if (UIManager.Instance != null && UIManager.Instance.HasActiveModalOrWindow()) return;

                if (this != null && isActiveAndEnabled && !string.IsNullOrEmpty(InteractionPrompt))
                {
                    hudView.ShowInteractionPrompt(InteractionPrompt);
                }
            });
        }
    }
}