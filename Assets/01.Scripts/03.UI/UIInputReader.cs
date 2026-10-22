using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Util = DebugLogger<UIInputReader>;

/// <summary>
/// UI 액션 맵(Navigate, Submit, Cancel, Point, Click 등)의 입력을 중계하고,
/// 마우스 커서 가시성 제어 및 인벤토리 토글 이벤트를 제공하는 클래스
/// </summary>
public class UIInputReader : IDisposable, PlayerInput.IUIActions
{
    private PlayerInput _inputActions;

    #region Action Events
    // 단축키 및 상태 이벤트
    public event Action OnCancelEvent;           // ESC / 뒤로 가기
    public event Action OnSubmitEvent;           // 결정 / 확인
    public event Action OnInventoryToggleEvent;   // Tab, I 인벤토리 토글

    // 마우스 및 포커스 프로퍼티
    public Vector2 MousePosition { get; private set; }
    public Vector2 NavigationInput { get; private set; }
    #endregion

    public UIInputReader(PlayerInput inputActions = null)
    {
        // 외부에서 전달된 인스턴스가 없으면 새로 생성
        _inputActions = inputActions ?? new PlayerInput();
        _inputActions.UI.SetCallbacks(this);
    }

    /// <summary>
    /// UI 조작 활성화 및 마우스 커서 해제
    /// </summary>
    public void EnableUIInput()
    {
        _inputActions.Player.Disable();
        _inputActions.UI.Enable();

        // UI 창이 열렸으므로 마우스 커서를 화면에 표시하고 잠금 해제
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Util.Log("[UI 조작권 활성화] 마우스 커서 활성화");
    }

    /// <summary>
    /// 게임플레이 조작 복귀 및 마우스 커서 잠금
    /// </summary>
    public void EnablePlayerInput()
    {
        _inputActions.UI.Disable();
        _inputActions.Player.Enable();

        // 3D 소울라이크 카메라 시점 회전을 위해 마우스 커서를 중앙에 고정 및 숨김
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Util.Log("[Player 조작권 복귀] 마우스 커서 잠금");
    }

    #region UIActions 인터페이스 구현부
    public void OnCancel(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnCancelEvent?.Invoke();
        }
    }

    public void OnSubmit(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnSubmitEvent?.Invoke();
        }
    }

    public void OnNavigate(InputAction.CallbackContext context)
    {
        NavigationInput = context.ReadValue<Vector2>();
    }

    public void OnPoint(InputAction.CallbackContext context)
    {
        MousePosition = context.ReadValue<Vector2>();
    }

    public void OnClick(InputAction.CallbackContext context) { }
    public void OnRightClick(InputAction.CallbackContext context) { }

    public void OnScrollWheel(InputAction.CallbackContext context) { }

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnInventoryToggleEvent?.Invoke();
        }
    }
    #endregion

    public void Dispose()
    {
        if (_inputActions != null)
        {
            _inputActions.UI.Disable();
            _inputActions.UI.SetCallbacks(null);
            _inputActions.Dispose();
            _inputActions = null;
        }
    }
}