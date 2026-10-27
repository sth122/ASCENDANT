using UnityEngine;
using Util = DebugLogger<GameManager>;

/// <summary>
/// 게임의 거시적 진행 상태(GameState)와 물리 시간(TimeScale)을 총괄 제어하는 중앙 매니저
/// </summary>
public class GameManager : MonoSingleton<GameManager>
{
    [Header("Current Runtime State")]
    [SerializeField] private GameState _currentState = GameState.None;

    public GameState CurrentState => _currentState;
    public bool IsPaused => _currentState == GameState.Paused;
    public bool IsPlaying => _currentState == GameState.Playing;

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();
    }

    /// <summary>
    /// 게임 상태를 안전하게 전이시키고 관련 시스템(시간, 입력 맵)을 동기화
    /// </summary>
    public void SetState(GameState newState)
    {
        if (_currentState == newState) return;

        GameState previousState = _currentState;
        _currentState = newState;

        Util.Log($"[GameState 전이] {previousState} -> {newState}");

        ApplyStatePolicy(newState);

        // 전역 이벤트 버스에 상태 변경 브로드캐스트
        EventBusSystem.Publish(new GameStateChangedEvent(previousState, newState));
    }

    /// <summary>
    /// 상태에 따른 엔진 시간 스케일 및 기본 정책 적용
    /// </summary>
    private void ApplyStatePolicy(GameState state)
    {
        switch (state)
        {
            case GameState.Playing:
                Time.timeScale = 1.0f;
                if (UIManager.Instance != null && UIManager.Instance.InputReader != null)
                {
                    UIManager.Instance.InputReader.EnablePlayerInput();
                }
                break;

            case GameState.Paused:
                Time.timeScale = 0f;
                if (UIManager.Instance != null && UIManager.Instance.InputReader != null)
                {
                    UIManager.Instance.InputReader.EnableUIInput();
                }
                break;

            case GameState.Cinematic:
                Time.timeScale = 1.0f;
                // 플레이어 조작은 잠그고 커서는 숨김
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;

            case GameState.GameOver:
                Time.timeScale = 0.5f; // 소울라이크 특유의 슬로우 사망 연출
                break;

            case GameState.Title:
                Time.timeScale = 1.0f;
                if (UIManager.Instance != null && UIManager.Instance.InputReader != null)
                {
                    UIManager.Instance.InputReader.EnableUIInput();
                }
                break;
        }
    }

    #region 편의 API
    /// <summary>
    /// 인게임 일시정지 및 시스템 메뉴 요청
    /// </summary>
    public void PauseGame()
    {
        if (_currentState != GameState.Playing) return;
        SetState(GameState.Paused);
    }

    /// <summary>
    /// 게임 플레이 재개
    /// </summary>
    public void ResumeGame()
    {
        if (_currentState != GameState.Paused) return;
        SetState(GameState.Playing);
    }

    /// <summary>
    /// 플레이어 사망 시 호출
    /// </summary>
    public void TriggerGameOver()
    {
        SetState(GameState.GameOver);
    }
    #endregion
}