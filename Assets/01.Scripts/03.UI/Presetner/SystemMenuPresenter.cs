using Cysharp.Threading.Tasks;
using Util = DebugLogger<SystemMenuPresenter>;

/// <summary>
/// ESC 시스템 메뉴 창의 Presenter
/// 버튼 입력을 수신하여 창 닫기, 타이틀 씬 전환, 게임 종료를 중계
/// </summary>
public class SystemMenuPresenter : UIPresenterBase<SystemMenuView, SystemMenuModel>
{
    protected override void OnBind()
    {
        OnBindHandler();
        Util.Log("시스템 메뉴 바인딩 완료");
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
        Util.Log("시스템 메뉴 언바인드 완료");
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();

        if (View.ResumeButton != null)
        {
            View.ResumeButton.onClick.AddListener(HandleResumeClicked);
        }

        if (View.TitleButton != null)
        {
            View.TitleButton.onClick.AddListener(HandleTitleClicked);
        }

        if (View.QuitButton != null)
        {
            View.QuitButton.onClick.AddListener(HandleQuitClicked);
        }
    }

    protected override void OnUnbindHandler()
    {
        if (View == null) return;

        if (View.ResumeButton != null)
        {
            View.ResumeButton.onClick.RemoveListener(HandleResumeClicked);
        }

        if (View.TitleButton != null)
        {
            View.TitleButton.onClick.RemoveListener(HandleTitleClicked);
        }

        if (View.QuitButton != null)
        {
            View.QuitButton.onClick.RemoveListener(HandleQuitClicked);
        }
    }

    /// <summary>
    /// 게임 재개 버튼 클릭 시 창 닫기
    /// </summary>
    private void HandleResumeClicked()
    {
        if (View != null)
        {
            View.CloseWindowAsync().Forget();
        }
    }

    /// <summary>
    /// 타이틀 씬 복귀 : 가장 최근 화롯불 상태를 유지한 채 복귀
    /// </summary>
    private void HandleTitleClicked()
    {
        Util.Log("TitleScene 전환 요청");

        // 1. 현재 창 닫기 및 시간 스케일 복원
        if (View != null)
        {
            View.CloseWindowAsync().Forget();
        }

        UnityEngine.Time.timeScale = 1.0f;


        // 2. TitleScene 전환 파이프라인 가동
        if (SceneLoadManager.Instance != null)
        {
            SceneLoadManager.Instance.LoadSceneFlowAsync(SceneId.TitleScene).Forget();
        }
    }

    /// <summary>
    /// 게임 종료 버튼 클릭
    /// </summary>
    private void HandleQuitClicked()
    {
        Util.Log("게임 종료 실행");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        UnityEngine.Application.Quit();
#endif
    }
}