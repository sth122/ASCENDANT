using Cysharp.Threading.Tasks;

public class BonfirePresenter : UIPresenterBase<BonfireWindowView, BonfireModel>
{
    private PlayerController _player;

    protected override void OnBind()
    {
        if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned)
        {
            _player = provider.Player;
        }

        OnBindHandler();
        ResetVirtualStats();
    }

    protected override void OnUnbind()
    {
        OnUnbindHandler();
        _player = null;
    }

    protected override void OnBindHandler()
    {
        OnUnbindHandler();
        if (View == null) return;

        View.RestButton?.onClick.AddListener(HandleRestClicked);
        View.LevelUpMenuButton?.onClick.AddListener(HandleLevelUpMenuClicked);
        View.LeaveButton?.onClick.AddListener(HandleLeaveClicked);

        View.VigPlusBtn?.onClick.AddListener(() => TryAllocateStat(ref Model.AddVig));
        View.EndPlusBtn?.onClick.AddListener(() => TryAllocateStat(ref Model.AddEnd));
        View.StrPlusBtn?.onClick.AddListener(() => TryAllocateStat(ref Model.AddStr));
        View.DexPlusBtn?.onClick.AddListener(() => TryAllocateStat(ref Model.AddDex));

        View.ConfirmLevelUpBtn?.onClick.AddListener(HandleConfirmLevelUp);
    }

    protected override void OnUnbindHandler()
    {
        if (View == null) return;
        View.RestButton?.onClick.RemoveAllListeners();
        View.LevelUpMenuButton?.onClick.RemoveAllListeners();
        View.LeaveButton?.onClick.RemoveAllListeners();
        View.VigPlusBtn?.onClick.RemoveAllListeners();
        View.EndPlusBtn?.onClick.RemoveAllListeners();
        View.StrPlusBtn?.onClick.RemoveAllListeners();
        View.DexPlusBtn?.onClick.RemoveAllListeners();
        View.ConfirmLevelUpBtn?.onClick.RemoveAllListeners();
    }

    private void HandleRestClicked()
    {
        if (_player == null) return;

        // 1. 플레이어 HP / 스태미나 완충
        _player.StatusController.HealHp(_player.StatusController.MaxHp);
        _player.StatusController.RestoreStamina(_player.StatusController.MaxStamina);

        // 2. 화톳불 위치 및 인벤토리 원자적 세이브
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.UpdateCheckpoint(_player.transform.position, _player.transform.eulerAngles.y,
                SceneLoadManager.Instance.CurrentScene, _player);
        }

        // 3. 월드 에너미 리셋 전역 신호 발행
        EventBusSystem.Publish(new BonfireRestedEvent(_player.transform.position));
        DebugLogger.LogWithTag("Bonfire", "[화톳불 휴식 완료: HP 회복 및 월드 몬스터 리셋]");
    }

    private void HandleLevelUpMenuClicked()
    {
        if (View == null) return;
        bool isOpen = !View.LevelUpPanel.activeSelf;
        View.LevelUpPanel.SetActive(isOpen);
        if (isOpen) ResetVirtualStats();
    }

    private void ResetVirtualStats()
    {
        if (_player == null || Model == null) return;

        PlayerInfo info = _player.StatusController.PlayerInfoSource;
        Model.BaseLevel = info.SoulLevel;
        Model.TargetLevel = info.SoulLevel;
        Model.CurrentSouls = info.CurrentSouls;
        Model.Reset();

        RefreshView();
    }

    private int CalculateRequiredSouls(int level)
    {
        // 다크소울 표준 소울 레벨업 수식
        return UnityEngine.Mathf.RoundToInt(0.02f * UnityEngine.Mathf.Pow(level, 3) + 3.06f * UnityEngine.Mathf.Pow(level, 2) + 105.6f * level - 52);
    }

    private void TryAllocateStat(ref int targetAddStat)
    {
        int nextReq = CalculateRequiredSouls(Model.TargetLevel);
        if (Model.CurrentSouls >= Model.RequiredSouls + nextReq)
        {
            Model.RequiredSouls += nextReq;
            Model.TargetLevel++;
            targetAddStat++;
            RefreshView();
        }
    }

    private void RefreshView()
    {
        if (View == null || _player == null) return;
        PlayerInfo info = _player.StatusController.PlayerInfoSource;

        if (View.CurrentSoulText != null) View.CurrentSoulText.text = Model.CurrentSouls.ToString("N0");
        if (View.RequiredSoulText != null) View.RequiredSoulText.text = Model.RequiredSouls.ToString("N0");
        if (View.TargetLevelText != null) View.TargetLevelText.text = $"Level: {Model.BaseLevel} -> {Model.TargetLevel}";

        if (View.VigValText != null) View.VigValText.text = $"{info.Vigor} (+{Model.AddVig})";
        if (View.EndValText != null) View.EndValText.text = $"{info.Endurance} (+{Model.AddEnd})";
        if (View.StrValText != null) View.StrValText.text = $"{info.Strength} (+{Model.AddStr})";
        if (View.DexValText != null) View.DexValText.text = $"{info.Dexterity} (+{Model.AddDex})";
    }

    private void HandleConfirmLevelUp()
    {
        if (_player == null || Model.TargetLevel <= Model.BaseLevel) return;

        PlayerInfo info = _player.StatusController.PlayerInfoSource;

        // 소울 차감 및 베이스 스탯 영구 증강 커밋
        info.ConsumeSouls(Model.RequiredSouls);

        var upgradedStat = (PlayerInfo)info.Clone();
        // 리플렉션 없이 세이브 데이터 포맷 활용 커밋
        PlayerSaveData upgradeData = new PlayerSaveData(upgradedStat, _player.StatusController.CurrentHp)
        {
            SoulLevel = Model.TargetLevel,
            CurrentSouls = info.CurrentSouls,
            Vigor = info.Vigor + Model.AddVig,
            Endurance = info.Endurance + Model.AddEnd,
            Strength = info.Strength + Model.AddStr,
            Dexterity = info.Dexterity + Model.AddDex
        };
        upgradedStat.ApplySaveData(upgradeData);

        // 런타임 스탯 컨트롤러 동기화 및 HP 완충
        _player.StatusController.SyncRuntimeStats(upgradedStat);
        _player.StatusController.HealHp(_player.StatusController.MaxHp);

        // 장비 스탯 재계산
        if (_player.TryGetComponent(out PlayerInventoryHandler handler))
        {
            handler.InventoryInstance.RecalculateEquipStats(_player);
        }

        // 즉각 영속 저장
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.UpdateCheckpoint(_player.transform.position, _player.transform.eulerAngles.y,
                SceneLoadManager.Instance.CurrentScene, _player);
        }

        ResetVirtualStats();
        DebugLogger.LogWithTag("Bonfire", $"[레벨업 완료] 달성 레벨: {Model.BaseLevel}");
    }

    private void HandleLeaveClicked()
    {
        View?.CloseWindowAsync().Forget();
    }
}