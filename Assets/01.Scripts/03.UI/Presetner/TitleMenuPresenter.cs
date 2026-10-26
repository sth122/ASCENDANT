using Cysharp.Threading.Tasks;
using Util = DebugLogger<TitleMenuPresenter>;
/// <summary>
/// 타이틀 화면의 버튼 액션과 씬 전환을 중계하는 프리젠터
/// </summary>
public class TitleMenuPresenter : UIPresenterBase<TitleMenuView, TitleMenuModel>
{
    protected override void OnBind()
    {
        if (View == null) return;

        // 세이브 데이터 존재 여부 갱신
        Model.RefreshSaveState();
        View.SetContinueInteractable(Model.CanContinueGame);

        // 버튼 이벤트 등록
        OnBindHandler();

        Util.Log("[타이틀 메뉴 Presenter 바인딩 완료]");
    }

    protected override void OnUnbind()
    {
        if (View == null) return;

        // 버튼 이벤트 해제
        OnUnbindHandler();

        Util.Log("[타이틀 메뉴 Presenter 해제 완료]");
    }

    #region 이벤트 Bind & Unbind
    protected override void OnBindHandler()
    {
        OnUnbindHandler();

        if (View.NewGameButton != null)
        {
            View.NewGameButton.onClick.AddListener(HandleNewGameClicked);
        }
        if (View.ContinueButton != null)
        {
            View.ContinueButton.onClick.AddListener(HandleContinueClicked);
        }
        if (View.QuitButton != null)
        {
            View.QuitButton.onClick.AddListener(HandleQuitClicked);
        }
    }

    protected override void OnUnbindHandler()
    {
        if (View.NewGameButton != null)
        {
            View.NewGameButton.onClick.RemoveListener(HandleNewGameClicked);
        }
        if (View.ContinueButton != null)
        {
            View.ContinueButton.onClick.RemoveListener(HandleContinueClicked);
        }
        if (View.QuitButton != null)
        {
            View.QuitButton.onClick.RemoveListener(HandleQuitClicked);
        }
    }
    #endregion

    private void HandleNewGameClicked()
    {
        Util.Log("새 게임 시작: 인게임 씬 전환 호출");

        // 신규 세이브 데이터 슬롯 0번 생성
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.CreateNewGameSave(0);
        }

        // GameScene으로 전환
        if (SceneLoadManager.Instance != null)
        {
            SceneLoadManager.Instance.LoadSceneFlowAsync(SceneId.GameScene).Forget();
        }
    }
    private void HandleContinueClicked()
    {
        if (SaveManager.Instance == null || !SaveManager.Instance.HasSaveData(0))
        {
            Util.LogWarning("불러올 세이브 데이터가 없습니다.");
            return;
        }

        Util.Log("[이어하기] 기존 세이브 데이터 로드 후 씬 로드");

        bool loadSuccess = SaveManager.Instance.LoadGame(0);
        if (loadSuccess && SaveManager.Instance.CurrentSaveData != null)
        {
            SceneId targetScene = SaveManager.Instance.CurrentSaveData.LastSavedScene;

            if (SceneLoadManager.Instance != null)
            {
                SceneLoadManager.Instance.LoadSceneFlowAsync(targetScene).Forget();
            }
        }
    }

    private void HandleQuitClicked()
    {
        Util.Log("게임 종료 요청");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        UnityEngine.Application.Quit();
#endif
    }
}