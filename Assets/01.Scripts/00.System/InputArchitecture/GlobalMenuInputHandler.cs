using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Util = DebugLogger<GlobalMenuInputHandler>;

/// <summary>
/// 인게임 플레이 중 UI 단축키(ESC 시스템 메뉴, I 인벤토리) 입력을 전담 수신하여 
/// UIManager에 창 개폐를 요청하는 전용 입력 핸들러 컴포넌트
/// </summary>
public class GlobalMenuInputHandler : MonoBehaviour
{
    private void Update()
    {
        HandleKeyboardShortcuts();
    }

    private void HandleKeyboardShortcuts()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 1. ESC 키 입력 처리: 오직 "인게임 플레이 중"이고 "열린 창이 없을 때"만 시스템 메뉴를 염
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
            {
                if (UIManager.Instance != null && !UIManager.Instance.HasActiveModalOrWindow())
                {
                    OpenSystemMenuAsync().Forget();
                }
            }
            // 주의: 이미 창이 열려 있는 상태(Paused)에서는 UIInputReader의 Cancel 액션이 처리하므로 여기서 절대 개입하지 않음
        }

        // 2. I 키 입력 처리 (인벤토리)
        if (keyboard.iKey.wasPressedThisFrame)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
            {
                Util.Log("인벤토리 창 토글 요청");
            }
        }
    }

    private async UniTaskVoid OpenSystemMenuAsync()
    {
        SystemMenuModel model = new SystemMenuModel();
        await UIManager.Instance.OpenUIAsync<SystemMenuView, SystemMenuPresenter, SystemMenuModel>(
            UIId.SystemMenuUI, "SystemMenuUI", model, this.destroyCancellationToken
        );
    }
}