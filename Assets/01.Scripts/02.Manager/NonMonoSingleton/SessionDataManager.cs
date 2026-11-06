using Util = DebugLogger<SessionDataManager>;
/// <summary>
/// 씬 전환 간 플레이어의 런타임 상태(현재 체력, 소울, 인벤토리 등)를 일시 보존하는 인메모리 세션 매니저
/// </summary>
public class SessionDataManager : NonMonoSingleton<SessionDataManager>
{
    public PlayerSaveData CurrentSessionData { get; private set; }
    public bool HasActiveSession => CurrentSessionData != null;

    /// <summary>
    /// 새 게임 시작 시 기본 스탯 정보로부터 세션 생성
    /// </summary>
    public void InitializeNewSession(PlayerInfo baseStat)
    {
        if (baseStat == null) return;

        CurrentSessionData = new PlayerSaveData(baseStat, baseStat.CalculateMaxHp());
    }

    /// <summary>
    /// 세이브 파일 로드 시 영속 데이터로부터 세션 복원
    /// </summary>
    public void LoadSessionFromSave(PlayerSaveData saveData)
    {
        if (saveData == null) return;

        CurrentSessionData = saveData;
    }

    /// <summary>
    /// 씬 전환 직전 현재 플레이어의 실시간 상태를 세션에 동기화
    /// </summary>
    public void SyncFromPlayer(PlayerController player)
    {
        if (player == null || player.StatusController == null) return;

        PlayerInfo info = player.StatusController.PlayerInfoSource;
        float curHp = player.StatusController.CurrentHp;

        CurrentSessionData = new PlayerSaveData(info, curHp);

        if (player.TryGetComponent(out PlayerInventoryHandler handler) && handler.InventoryInstance != null)
        {
            var inv = handler.InventoryInstance;
            CurrentSessionData.EquippedWeaponId = inv.GetEquippedItem(EquipSlotType.MainWeapon)?.Data.Id ?? 0;
            CurrentSessionData.EquippedShieldId = inv.GetEquippedItem(EquipSlotType.Shield)?.Data.Id ?? 0;
            CurrentSessionData.EquippedHelmetId = inv.GetEquippedItem(EquipSlotType.Helmet)?.Data.Id ?? 0;
            CurrentSessionData.EquippedChestArmorId = inv.GetEquippedItem(EquipSlotType.ChestArmor)?.Data.Id ?? 0;
            CurrentSessionData.EquippedLeggingsId = inv.GetEquippedItem(EquipSlotType.Leggings)?.Data.Id ?? 0;
            CurrentSessionData.EquippedBootsId = inv.GetEquippedItem(EquipSlotType.Boots)?.Data.Id ?? 0;
            CurrentSessionData.HasReceivedInitialEquipment = true;
        }
    }

    public void ClearSession()
    {
        CurrentSessionData = null;
    }
}