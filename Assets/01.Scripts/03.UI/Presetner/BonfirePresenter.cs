using Cysharp.Threading.Tasks;

public class BonfirePresenter : UIPresenterBase<BonfireWindowView, BonfireModel>
{
    private PlayerController _player;
    private bool _isConfirming = false;

    protected override void OnBind()
    {
        if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned)
        {
            _player = provider.Player;
        }

        if (_player != null)
        {
            Model.CaptureFromPlayer(_player);
            View.RefreshDisplay(Model);
        }

        OnBindHandler();
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
        _player = null;
        Model?.Reset();
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();
        if (View == null) return;

        View.VigorPlusBtn?.onClick.AddListener(HandleVigorPlus);
        View.VigorMinusBtn?.onClick.AddListener(HandleVigorMinus);
        View.EndurancePlusBtn?.onClick.AddListener(HandleEndurancePlus);
        View.EnduranceMinusBtn?.onClick.AddListener(HandleEnduranceMinus);
        View.StrengthPlusBtn?.onClick.AddListener(HandleStrengthPlus);
        View.StrengthMinusBtn?.onClick.AddListener(HandleStrengthMinus);
        View.DexterityPlusBtn?.onClick.AddListener(HandleDexterityPlus);
        View.DexterityMinusBtn?.onClick.AddListener(HandleDexterityMinus);

        View.ConfirmLevelUpBtn?.onClick.AddListener(HandleConfirmLevelUpClicked);
        View.CloseLevelUpPanelBtn?.onClick.AddListener(HandleCloseClicked);
    }

    protected override void OnUnbindHandler()
    {
        if (View == null) return;

        View.VigorPlusBtn?.onClick.RemoveListener(HandleVigorPlus);
        View.VigorMinusBtn?.onClick.RemoveListener(HandleVigorMinus);
        View.EndurancePlusBtn?.onClick.RemoveListener(HandleEndurancePlus);
        View.EnduranceMinusBtn?.onClick.RemoveListener(HandleEnduranceMinus);
        View.StrengthPlusBtn?.onClick.RemoveListener(HandleStrengthPlus);
        View.StrengthMinusBtn?.onClick.RemoveListener(HandleStrengthMinus);
        View.DexterityPlusBtn?.onClick.RemoveListener(HandleDexterityPlus);
        View.DexterityMinusBtn?.onClick.RemoveListener(HandleDexterityMinus);

        View.ConfirmLevelUpBtn?.onClick.RemoveListener(HandleConfirmLevelUpClicked);
        View.CloseLevelUpPanelBtn?.onClick.RemoveListener(HandleCloseClicked);
    }

    private void RecalculateAndRefresh()
    {
        Model.RecalculatePreviewStats(_player);
        View.RefreshDisplay(Model);
    }

    private void HandleVigorPlus() { if (Model.CanAffordNextPoint()) { Model.PendingVigor++; RecalculateAndRefresh(); } }
    private void HandleVigorMinus() { if (Model.PendingVigor > 0) { Model.PendingVigor--; RecalculateAndRefresh(); } }
    private void HandleEndurancePlus() { if (Model.CanAffordNextPoint()) { Model.PendingEndurance++; RecalculateAndRefresh(); } }
    private void HandleEnduranceMinus() { if (Model.PendingEndurance > 0) { Model.PendingEndurance--; RecalculateAndRefresh(); } }
    private void HandleStrengthPlus() { if (Model.CanAffordNextPoint()) { Model.PendingStrength++; RecalculateAndRefresh(); } }
    private void HandleStrengthMinus() { if (Model.PendingStrength > 0) { Model.PendingStrength--; RecalculateAndRefresh(); } }
    private void HandleDexterityPlus() { if (Model.CanAffordNextPoint()) { Model.PendingDexterity++; RecalculateAndRefresh(); } }
    private void HandleDexterityMinus() { if (Model.PendingDexterity > 0) { Model.PendingDexterity--; RecalculateAndRefresh(); } }

    private void HandleConfirmLevelUpClicked()
    {
        if (_isConfirming || _player == null || Model.TotalPendingPoints <= 0) return;
        ExecuteConfirmLevelUpFlowAsync().Forget();
    }

    private async UniTaskVoid ExecuteConfirmLevelUpFlowAsync()
    {
        _isConfirming = true;
        try
        {
            int totalCost = Model.CalculateTotalSoulCost();
            if (UIManager.Instance == null) return;

            ConfirmPopupView popup = await UIManager.Instance.OpenUIAsync<ConfirmPopupView, ConfirmPopupPresenter, ConfirmPopupModel>(
                UIId.ConfirmPopup);
            if (popup == null) return;

            popup.SetMessage($"{totalCost:N0} 소울로 레벨업 합니까?");
            bool isConfirmed = await popup.WaitForResultAsync();
            if (!isConfirmed) return;

            PlayerStatusController status = _player.StatusController;
            PlayerInfo info = status.PlayerInfoSource;
            if (info == null || !status.TryConsumeSouls(totalCost)) return;

            info.ApplyAttributeUpgrades(Model.PendingVigor, Model.PendingEndurance, Model.PendingStrength, Model.PendingDexterity);
            status.SyncRuntimeStats(info);
            status.HealHp(status.MaxHp);
            status.RestoreStamina(status.MaxStamina);

            if (_player.TryGetComponent(out PlayerInventoryHandler invHandler) && invHandler.InventoryInstance != null)
            {
                invHandler.InventoryInstance.RecalculateEquipStats(_player);
            }

            if (SaveManager.Instance != null)
            {
                BonfireId bonfireId = SaveManager.Instance.CurrentSaveData != null ? SaveManager.Instance.CurrentSaveData.LastBonfireId : BonfireId.None;
                SceneId sceneId = SceneLoadManager.Instance != null ? SceneLoadManager.Instance.CurrentScene : SceneId.GameScene;
                SaveManager.Instance.UpdateCheckpoint(bonfireId, _player.transform.position, _player.transform.eulerAngles.y, sceneId, _player);
            }

            SoundManager.Instance?.PlaySFX(SoundId.UI_Confirm);

            Model.CaptureFromPlayer(_player);
            View.RefreshDisplay(Model);
        }
        finally
        {
            _isConfirming = false;
        }
    }

    private void HandleCloseClicked() => View?.CloseWindowAsync().Forget();
}