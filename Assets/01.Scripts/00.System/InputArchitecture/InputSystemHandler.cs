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

    public static event Action OnInventoryToggleRequested;
    public static event Action OnCancelRequested;
    public static event Action OnTitleQuitRequested;

    public static void Initialize()
    {
        if (_actions == null)
        {
            _actions = new PlayerInput();
            _actions.Player.Inventory.performed += ctx => HandleInventoryInput();
            _actions.UI.Inventory.performed += ctx => HandleInventoryInput();
            _actions.UI.Cancel.performed += ctx => HandleCancelInput();
        }

        SetMode(CurrentMode);
    }

    private static void HandleInventoryInput()
    {
        // 타이틀 화면이거나 인게임 플레이 상태가 아닐 때는 인벤토리 개폐 불가
        if (CurrentMode == InputMode.Title) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        OnInventoryToggleRequested?.Invoke();
    }

    private static void HandleCancelInput()
    {
        // 타이틀 화면에서 ESC를 누르면 창이 꺼지는 대신 게임 종료 확인 팝업 실행
        if (CurrentMode == InputMode.Title)
        {
            OnTitleQuitRequested?.Invoke();
            return;
        }

        OnCancelRequested?.Invoke();
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