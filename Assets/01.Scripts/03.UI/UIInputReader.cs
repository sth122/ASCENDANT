using System;
using UnityEngine;
using UnityEngine.InputSystem;

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
    public event Action OnInventoryCloseEvent;   // I 인벤토리 토글

    // 마우스 및 포커스 프로퍼티
    public Vector2 MousePosition { get; private set; }
    public Vector2 NavigationInput { get; private set; }
    #endregion

    public UIInputReader()
    {
        _inputActions = InputSystemHandler.Actions;
        _inputActions.UI.SetCallbacks(this);
    }

    public void EnableUIInput() => InputSystemHandler.SetMode(InputMode.RealtimeUI);
    public void EnablePlayerInput() => InputSystemHandler.SetMode(InputMode.Gameplay);

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

    public void OnNavigate(InputAction.CallbackContext context) => NavigationInput = context.ReadValue<Vector2>();

    public void OnPoint(InputAction.CallbackContext context) => MousePosition = context.ReadValue<Vector2>();

    public void OnClick(InputAction.CallbackContext context) { }
    public void OnRightClick(InputAction.CallbackContext context) { }
    public void OnScrollWheel(InputAction.CallbackContext context) { }

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnInventoryCloseEvent?.Invoke();
        }
    }
    #endregion

    public void Dispose()
    {
        if (_inputActions != null)
        {
            _inputActions.UI.SetCallbacks(null);
            _inputActions = null;
        }
    }
}