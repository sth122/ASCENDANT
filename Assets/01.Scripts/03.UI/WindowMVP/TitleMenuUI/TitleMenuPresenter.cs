using ConstStrings;
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

        // 버튼 이벤트 등록
        OnBindHandler();
    }

    protected override void OnUnbind()
    {
        if (View == null) return;

        // 버튼 이벤트 해제
        OnUnbindHandler();
    }

    #region 이벤트 Bind & Unbind
    protected override void OnBindHandler()
    {
        OnUnbindHandler();

        if (View.StartButton != null)
        {
            View.StartButton.onClick.AddListener(HandleStartClicked);
        }
        if (View.SettingsButton != null)
        {
            View.SettingsButton.onClick.AddListener(HandleSettingsClicked);
        }
        if (View.QuitButton != null)
        {
            View.QuitButton.onClick.AddListener(PromptQuitGame);
        }
    }

    protected override void OnUnbindHandler()
    {
        if (View.StartButton != null)
        {
            View.StartButton.onClick.RemoveListener(HandleStartClicked);
        }
        if (View.SettingsButton != null)
        {
            View.SettingsButton.onClick.RemoveListener(HandleSettingsClicked);
        }
        if (View.QuitButton != null)
        {
            View.QuitButton.onClick.RemoveListener(PromptQuitGame);
        }
    }
    #endregion

    private void HandleStartClicked() => OpenCharacterSelectAsync().Forget(); 
    private void HandleSettingsClicked() => OpenSettingsAsync().Forget();
    public void PromptQuitGame() => ExecuteQuitConfirmFlowAsync().Forget();

    private async UniTaskVoid OpenCharacterSelectAsync()
    {
        if (UIManager.Instance == null) return;

        await UIManager.Instance.OpenUIAsync<CharacterSelectView, CharacterSelectPresenter, CharacterSelectModel>(
            UIId.CharacterSelectUI);
    }

    private async UniTaskVoid OpenSettingsAsync()
    {
        if (UIManager.Instance == null) return;

        await UIManager.Instance.OpenUIAsync<SettingView, SettingPresenter, SettingModel>(
            UIId.SettingUI);
    }

    private async UniTaskVoid ExecuteQuitConfirmFlowAsync()
    {
        bool isConfirmed = await OpenConfirmDialogAsync(ConstString.GameQuitPrompt);
        if (!isConfirmed) return;

        Util.Log("게임 종료 승인");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        UnityEngine.Application.Quit();
#endif
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