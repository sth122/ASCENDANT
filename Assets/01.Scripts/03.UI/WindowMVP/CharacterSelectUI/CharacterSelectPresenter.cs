using Cysharp.Threading.Tasks;

public class CharacterSelectPresenter : UIPresenterBase<CharacterSelectView, CharacterSelectModel>
{
    private int _pendingCreateSlotIndex = 0;

    protected override void OnBind()
    {
        if (View == null || Model == null) return;

        Model.LoadInitialStatsPreview();
        Model.RefreshSlots();
        Model.CurrentSlotIndex = 0;

        View.ShowSelectionWindow();
        View.RenderSelectionSlots(Model);

        OnBindHandler();
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
        Model?.Reset();
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();
        if (View == null) return;

        if (View.PrevSlotButton != null) View.PrevSlotButton.onClick.AddListener(HandlePrevSlot);
        if (View.NextSlotButton != null) View.NextSlotButton.onClick.AddListener(HandleNextSlot);
        if (View.PlayButton != null) View.PlayButton.onClick.AddListener(HandlePlayClicked);
        if (View.OpenCreateButton != null) View.OpenCreateButton.onClick.AddListener(HandleOpenCreateClicked);
        if (View.OpenDeleteButton != null) View.OpenDeleteButton.onClick.AddListener(HandleOpenDeleteClicked);
        if (View.CloseMenuButton != null) View.CloseMenuButton.onClick.AddListener(HandleCloseMenuClicked);

        if (View.ConfirmCreateButton != null) View.ConfirmCreateButton.onClick.AddListener(HandleConfirmCreateClicked);
        if (View.CreationBackButton != null) View.CreationBackButton.onClick.AddListener(HandleCreationBackClicked);

        if (View.DeleteYesButton != null) View.DeleteYesButton.onClick.AddListener(HandleDeleteYesClicked);
        if (View.DeleteNoButton != null) View.DeleteNoButton.onClick.AddListener(HandleDeleteNoClicked);
    }

    protected override void OnUnbindHandler()
    {
        if (View == null) return;

        if (View.PrevSlotButton != null) View.PrevSlotButton.onClick.RemoveListener(HandlePrevSlot);
        if (View.NextSlotButton != null) View.NextSlotButton.onClick.RemoveListener(HandleNextSlot);
        if (View.PlayButton != null) View.PlayButton.onClick.RemoveListener(HandlePlayClicked);
        if (View.OpenCreateButton != null) View.OpenCreateButton.onClick.RemoveListener(HandleOpenCreateClicked);
        if (View.OpenDeleteButton != null) View.OpenDeleteButton.onClick.RemoveListener(HandleOpenDeleteClicked);
        if (View.CloseMenuButton != null) View.CloseMenuButton.onClick.RemoveListener(HandleCloseMenuClicked);

        if (View.ConfirmCreateButton != null) View.ConfirmCreateButton.onClick.RemoveListener(HandleConfirmCreateClicked);
        if (View.CreationBackButton != null) View.CreationBackButton.onClick.RemoveListener(HandleCreationBackClicked);

        if (View.DeleteYesButton != null) View.DeleteYesButton.onClick.RemoveListener(HandleDeleteYesClicked);
        if (View.DeleteNoButton != null) View.DeleteNoButton.onClick.RemoveListener(HandleDeleteNoClicked);
    }

    private void HandlePrevSlot()
    {
        Model.CurrentSlotIndex = (Model.CurrentSlotIndex + CharacterSelectModel.MaxCharacterSlots - 1) % CharacterSelectModel.MaxCharacterSlots;
        View.RenderSelectionSlots(Model);
    }

    private void HandleNextSlot()
    {
        Model.CurrentSlotIndex = (Model.CurrentSlotIndex + 1) % CharacterSelectModel.MaxCharacterSlots;
        View.RenderSelectionSlots(Model);
    }

    private void HandlePlayClicked()
    {
        int slot = Model.CurrentSlotIndex;
        if (!Model.HasCharacterInSlot(slot) || SaveManager.Instance == null || SceneLoadManager.Instance == null) return;

        if (SaveManager.Instance.LoadGame(slot) && SaveManager.Instance.CurrentSaveData != null)
        {
            SceneId targetScene = SaveManager.Instance.CurrentSaveData.LastSavedScene;
            SceneLoadManager.Instance.LoadSceneFlowAsync(targetScene).Forget();
        }
    }

    private void HandleOpenCreateClicked()
    {
        // 현재 보고 있는 슬롯이 비어있으면 그 슬롯을, 차있으면 첫 번째 빈 슬롯을 선택
        int targetSlot = !Model.HasCharacterInSlot(Model.CurrentSlotIndex)
            ? Model.CurrentSlotIndex
            : Model.FindFirstEmptySlot();

        if (targetSlot < 0) return;

        _pendingCreateSlotIndex = targetSlot;
        View.ShowCreationWindow(_pendingCreateSlotIndex, Model);
    }

    private void HandleConfirmCreateClicked()
    {
        if (SaveManager.Instance == null || SceneLoadManager.Instance == null) return;

        string inputName = View.NameInputField != null ? View.NameInputField.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(inputName))
        {
            inputName = $"잿빛 기사 {_pendingCreateSlotIndex + 1}";
        }

        SaveManager.Instance.CreateNewGameSave(_pendingCreateSlotIndex, inputName, portraitIndex: _pendingCreateSlotIndex);
        SceneLoadManager.Instance.LoadSceneFlowAsync(SceneId.GameScene).Forget();
    }

    private void HandleCreationBackClicked()
    {
        View.ShowSelectionWindow();
        View.RenderSelectionSlots(Model);
    }

    private void HandleOpenDeleteClicked()
    {
        int slot = Model.CurrentSlotIndex;
        if (!Model.HasCharacterInSlot(slot)) return;
        View.ShowDeletePopup(Model.Slots[slot]);
    }

    private void HandleDeleteYesClicked()
    {
        int slot = Model.CurrentSlotIndex;
        if (SaveManager.Instance != null && SaveManager.Instance.DeleteSaveData(slot))
        {
            Model.RefreshSlots();
            View.HideDeletePopup();
            View.RenderSelectionSlots(Model);
        }
    }

    private void HandleDeleteNoClicked() => View.HideDeletePopup();
    private void HandleCloseMenuClicked() => View?.CloseWindowAsync().Forget();
}