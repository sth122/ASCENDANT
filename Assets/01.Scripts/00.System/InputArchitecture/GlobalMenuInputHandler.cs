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
    private bool _isMenuToggling = false;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HandleEscapePressed();
        }
    }

    private void HandleEscapePressed()
    {
        if (_isMenuToggling) return;

        if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
        {
            if (UIManager.Instance != null && !UIManager.Instance.HasActiveModalOrWindow())
            {
                _isMenuToggling = true;
                OpenSystemMenuAsync().Forget();
            }
        }
    }

    private async UniTaskVoid OpenSystemMenuAsync()
    {
        try
        {
            SystemMenuModel model = new SystemMenuModel();
            await UIManager.Instance.OpenUIAsync<SystemMenuView, SystemMenuPresenter, SystemMenuModel>(
                UIId.SystemMenuUI, "SystemMenuUI", model, this.destroyCancellationToken
            );
        }
        finally
        {
            // 동일 프레임 더블 트리거(열리자마자 닫히는 현상) 방지를 위해 1프레임 대기 후 락 해제
            await UniTask.Yield();
            _isMenuToggling = false;
        }
    }
}