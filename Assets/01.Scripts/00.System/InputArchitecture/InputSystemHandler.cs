using UnityEngine;

public static class InputSystemHandler
{
    private static PlayerInput _actions;
    public static PlayerInput Actions => _actions ??= new PlayerInput();

    public static InputMode CurrentMode { get; private set; } = InputMode.Title;

    public static void Initialize()
    {
        if (_actions == null)
        {
            _actions = new PlayerInput();
        }

        SetMode(CurrentMode);
    }

    public static void SetMode(InputMode mode)
    {
        CurrentMode = mode;

        switch (mode)
        {
            case InputMode.Title:
                Time.timeScale = 1f;
                Actions.Player.Disable();
                Actions.UI.Enable();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                CameraManager.Instance?.SetCameraInputEnabled(false);
                break;

            case InputMode.Gameplay:
                Time.timeScale = 1f;
                Actions.UI.Disable();
                Actions.Player.Enable();
                Actions.UI.Cancel.Enable();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                CameraManager.Instance?.SetCameraInputEnabled(true);
                break;

            case InputMode.RealtimeUI:
                Time.timeScale = 1f;
                Actions.Player.Disable();
                Actions.UI.Enable();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                CameraManager.Instance?.SetCameraInputEnabled(false);
                break;

            case InputMode.PausedUI:
                Time.timeScale = 0f;
                Actions.Player.Disable();
                Actions.UI.Enable();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                CameraManager.Instance?.SetCameraInputEnabled(false);
                break;
        }

        DebugLogger.LogWithTag("InputSystemHandler", $"[입력 모드 전이] {mode} (TimeScale: {Time.timeScale}, Cursor: {Cursor.visible})");
    }

    public static void Dispose()
    {
        if (_actions != null)
        {
            _actions.Disable();
            _actions.Dispose();
            _actions = null;
        }
    }
}