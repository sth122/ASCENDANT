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
        ExecuteNewGameFlowAsync().Forget();
    }
    private void HandleContinueClicked()
    {
        ExecuteContinueFlowAsync().Forget();
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

    private async UniTaskVoid ExecuteNewGameFlowAsync()
    {
        bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSaveData(0);

        string promptMessage = hasSave
            ? "이전 데이터를 삭제하고 새로 시작하시겠습니까?"
            : "게임을 시작하시겠습니까?";

        bool isConfirmed = await OpenConfirmDialogAsync(promptMessage);
        if (!isConfirmed) return;

        Util.Log("[새 게임 확정] 세이브 파일 생성 및 GameScene 이동");

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.CreateNewGameSave(0);
        }

        if (SceneLoadManager.Instance != null)
        {
            SceneLoadManager.Instance.LoadSceneFlowAsync(SceneId.GameScene).Forget();
        }
    }

    private async UniTaskVoid ExecuteContinueFlowAsync()
    {
        if (SaveManager.Instance == null || !SaveManager.Instance.HasSaveData(0))
        {
            Util.LogWarning("불러올 세이브 데이터가 없습니다.");
            return;
        }

        bool isConfirmed = await OpenConfirmDialogAsync("게임을 불러오시겠습니까?");
        if (!isConfirmed) return;

        Util.Log("[이어하기] 세이브 데이터 로드 후 씬 이동");

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

    private async UniTask<bool> OpenConfirmDialogAsync(string message)
    {
        if (UIManager.Instance == null) return false;

        ConfirmPopupView popup = await UIManager.Instance.OpenUIAsync<ConfirmPopupView, ConfirmPopupPresenter, ConfirmPopupModel>(
            UIId.ConfirmPopup);

        if (popup != null)
        {
            popup.SetMessage(message);
            return await popup.WaitForResultAsync();
        }

        return false;
    }
}