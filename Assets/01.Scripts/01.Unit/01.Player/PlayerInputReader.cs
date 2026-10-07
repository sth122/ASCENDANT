using System;
using UnityEngine;

public class PlayerInputReader : IDisposable, PlayerInput.IPlayerActions
{
    private PlayerInput _inputActions;

    #region Input Properties
    // FSM Update에서 Input을 읽기 위한 Properties
    public Vector2 MoveInput { get; private set; }
    public bool IsSprinting { get; private set; }
    #endregion

    #region Input Action Events
    // 외부 ( Command 패턴, 사운드, 이펙트 등)에서 구독할 이벤트
    public event Action OnJumpEvent;
    public event Action OnRollEvent;
    public event Action OnAttackEvent;
    public event Action OnInteractEvent;
    #endregion

    private PlayerInputReader()
    {
        if (_inputActions == null)
        {
            _inputActions = new PlayerInput();
            _inputActions.Player.SetCallbacks(this);
        }
        EnablePlayerInput();
    }

    public void EnablePlayerInput()
    {
        _inputActions?.UI.Disable();
        _inputActions?.Player.Disable();
        _inputActions?.Player.Enable();
    }

    public void EnableUIInput()
    {
        _inputActions?.Player.Disable();
        _inputActions?.UI.Disable();
        _inputActions?.UI.Enable();
    }

    #region IPlayerActions 인터페이스 구현부
    /// <summary>
    /// WASD, 방향키 입력을 읽어 MoveInput에 저장
    /// </summary>
    public void OnMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    /// <summary>
    /// 달리기 (Shift) 누르거나 땠을 때 호출
    /// </summary>
    public void OnSprint(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            IsSprinting = true;
        }
        else if (context.canceled)
        {
            IsSprinting = false;
        }
    }

    /// <summary>
    /// 점프 (Space) 키 입력 시 호출
    /// </summary>
    public void OnJump(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnJumpEvent?.Invoke();
        }
    }

    /// <summary>
    /// 공격 키(좌 클릭) 입력 시 호출 → 커맨트 패턴 큐에 사용
    /// </summary>
    public void OnAttack(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnAttackEvent?.Invoke();
        }
    }

    /// <summary>
    /// 상호작용(E) 키 입력 시 호출
    /// </summary>
    public void OnInteract(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnInteractEvent?.Invoke();
        }
    }

    /// <summary>
    /// 구르기(Tap) 키 입력 시 호출
    /// </summary>
    /// <param name="context"></param>
    public void OnRoll(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnRollEvent?.Invoke();
        }
    }
    #endregion
    public void Dispose()
    {
        if (_inputActions != null)
        {
            _inputActions.Player.Disable();
            _inputActions.UI.Disable();
            _inputActions.Player.SetCallbacks(null);
            _inputActions.Dispose();
            _inputActions = null;
        }
    }
}
