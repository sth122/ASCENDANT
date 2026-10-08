/// <summary>
/// Player 조작 Input System 콜백을 받아 입력을 관리하고,
/// 버튼 입력을 InputBufferQueue 적재 및 이벤트를 중계하는 클래스
/// </summary>
public class PlayerInputReader : System.IDisposable, PlayerInput.IPlayerActions
{
    private PlayerInput _inputActions;
    public InputBufferQueue BufferQueue { get; private set; }

    #region Input Properties
    // FSM Update에서 Input을 읽기 위한 Properties
    public UnityEngine.Vector2 MoveInput { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsGuarding { get; private set; }
    public bool HasInteractInput { get; private set; }
    #endregion

    #region Input Action Events
    // 외부 ( Command 패턴, 사운드, 이펙트 등)에서 구독할 이벤트
    public event System.Action<InputCommandType> OnCommandInputEvent;
    public event System.Action OnInteractEvent;
    public event System.Action OnLockOnEvent;
    #endregion

    public PlayerInputReader(float bufferDuration = 0.25f)
    {
        BufferQueue = new InputBufferQueue(bufferDuration);

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
        MoveInput = context.ReadValue<UnityEngine.Vector2>();
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
            BufferQueue.EnqueueCommnad(InputCommandType.Jump);
            OnCommandInputEvent?.Invoke(InputCommandType.Jump);
        }
    }

    /// <summary>
    /// 공격 키(좌 클릭) 입력 시 호출 → 커맨트 패턴 큐에 사용
    /// </summary>
    public void OnAttack(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            BufferQueue.EnqueueCommnad(InputCommandType.Attack);
            OnCommandInputEvent?.Invoke(InputCommandType.Attack);
        }
    }

    /// <summary>
    /// 구르기(Tap) 키 입력 시 호출
    /// </summary>
    public void OnRoll(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            BufferQueue.EnqueueCommnad(InputCommandType.Roll);
            OnCommandInputEvent?.Invoke(InputCommandType.Roll);
        }
    }
    
    /// <summary>
    /// 패리(가드/ C) 키 입력 시 호출
    /// </summary>
    /// <param name="context"></param>
    public void OnParry(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if(context.performed)
        {
            IsGuarding = true;
            BufferQueue.EnqueueCommnad(InputCommandType.Parry);
            OnCommandInputEvent?.Invoke(InputCommandType.Parry);
        }
        else if (context.canceled)
        {
            IsGuarding = false;
        }
    }
    /// <summary>
    /// 상호작용(E) 키 입력 시 호출
    /// </summary>
    public void OnInteract(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            HasInteractInput = true;
            OnInteractEvent?.Invoke();
        }
    }

    /// <summary>
    /// 마우스 휠 키 입력 시 타켓 Lock-On
    /// </summary>
    /// <param name="context"></param>
    public void OnLockOn(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnLockOnEvent?.Invoke();
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
