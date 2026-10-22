using Cysharp.Threading.Tasks;

/// <summary>
/// 타이틀 화면의 버튼 액션과 씬 전환을 중계하는 프리젠터
/// </summary>
public class TitleMenuPresenter : UIPresenterBase<TitleMenuView, TitleMenuModel>
{
    protected override void OnBind()
    {
        if (View == null) return;

        // 세이브 데이터 존재 여부에 따라 이어하기 버튼 활성화 (예시)
        View.SetContinueInteractable(Model.CanContinueGame);

        // 버튼 이벤트 등록 (무명 메서드 지양 원칙 준수)
        if (View.NewGameButton != null)
            View.NewGameButton.onClick.AddListener(HandleNewGameClicked);

        if (View.QuitButton != null)
            View.QuitButton.onClick.AddListener(HandleQuitClicked);

        DebugLogger.LogWithTag("TitleMenuPresenter", "[타이틀 메뉴 Presenter 바인딩 완료]");
    }

    protected override void OnUnbind()
    {
        if (View == null) return;

        if (View.NewGameButton != null)
            View.NewGameButton.onClick.RemoveListener(HandleNewGameClicked);

        if (View.QuitButton != null)
            View.QuitButton.onClick.RemoveListener(HandleQuitClicked);

        DebugLogger.LogWithTag("TitleMenuPresenter", "[타이틀 메뉴 Presenter 해제 완료]");
    }

    private void HandleNewGameClicked()
    {
        DebugLogger.LogWithTag("TitleMenuPresenter", "새 게임 시작: 인게임 씬 전환 호출");

        // SceneLoadManager를 통해 게임 구역 씬으로 비동기 전환
        if (SceneLoadManager.Instance != null)
        {
            SceneLoadManager.Instance.LoadSceneFlowAsync(SceneId.GameScene).Forget();
        }
    }

    private void HandleQuitClicked()
    {
        DebugLogger.LogWithTag("TitleMenuPresenter", "게임 종료 요청");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}