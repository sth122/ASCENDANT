using System;
using UnityEngine;
public enum InputMode
{
    Title,          // 타이틀/로비 화면 (UI 조작만 허용, 인벤토리/전투 원천 차단)
    Gameplay,       // 인게임 정상 조작 (플레이어 FSM 활성, 마우스 커서 숨김/고정)
    RealtimeUI,     // 인벤토리, 화톳불 등 실시간 인게임 창 (마우스 커서 표시, 플레이어 이동/공격 잠금)
    PausedUI        // 시스템 메뉴 등 일시정지 창 (TimeScale = 0)
}

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
                break;

            case InputMode.Gameplay:
                Time.timeScale = 1f;
                Actions.UI.Disable();
                Actions.Player.Enable();
                Actions.UI.Cancel.Enable();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;

            case InputMode.RealtimeUI:
                Time.timeScale = 1f;
                Actions.Player.Disable();
                Actions.UI.Enable();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;

            case InputMode.PausedUI:
                Time.timeScale = 0f;
                Actions.Player.Disable();
                Actions.UI.Enable();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
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