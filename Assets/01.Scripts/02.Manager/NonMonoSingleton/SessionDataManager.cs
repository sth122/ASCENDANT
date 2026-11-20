using System.Collections.Generic;
using Util = DebugLogger<SessionDataManager>;

/// <summary>
/// 씬 전환 간 플레이어의 런타임 상태 및 월드 상호작용(상자/열쇠문/튜토리얼) 기록을 유지하는 인메모리 세션 매니저
/// </summary>
public class SessionDataManager : NonMonoSingleton<SessionDataManager>
{
    public PlayerSaveData CurrentSessionData { get; private set; }
    public bool HasActiveSession => CurrentSessionData != null;

    private readonly HashSet<string> _openedChestIds = new HashSet<string>();
    private readonly Dictionary<string, List<ItemSlotSaveData>> _chestRemainingMap = new Dictionary<string, List<ItemSlotSaveData>>();
    private readonly HashSet<string> _unlockedDoorIds = new HashSet<string>();
    public bool HasCompletedIntroTutorial { get; private set; } = false;

    public void InitializeNewSession(PlayerInfo baseStat)
    {
        if (baseStat == null) return;

        CurrentSessionData = new PlayerSaveData(baseStat, baseStat.CalculateMaxHp());
        _openedChestIds.Clear();
        _chestRemainingMap.Clear();
        _unlockedDoorIds.Clear();
        HasCompletedIntroTutorial = false;
    }

    public void LoadSessionFromSave(PlayerSaveData saveData)
    {
        if (saveData == null) return;
        CurrentSessionData = saveData;
    }

    public void LoadWorldStateFromSave(SaveData rootSave)
    {
        _openedChestIds.Clear();
        _chestRemainingMap.Clear();
        _unlockedDoorIds.Clear();
        HasCompletedIntroTutorial = false;

        if (rootSave == null) return;

        if (rootSave.OpenedChestIds != null)
        {
            for (int i = 0; i < rootSave.OpenedChestIds.Count; i++)
            {
                _openedChestIds.Add(rootSave.OpenedChestIds[i]);
            }
        }

        if (rootSave.ChestRemainingItems != null)
        {
            for (int i = 0; i < rootSave.ChestRemainingItems.Count; i++)
            {
                ChestRemainingItemSaveData entry = rootSave.ChestRemainingItems[i];
                if (string.IsNullOrEmpty(entry.ChestId)) continue;

                if (!_chestRemainingMap.TryGetValue(entry.ChestId, out List<ItemSlotSaveData> list))
                {
                    list = new List<ItemSlotSaveData>();
                    _chestRemainingMap[entry.ChestId] = list;
                }
                list.Add(new ItemSlotSaveData(entry.SlotIndex, entry.ItemId, entry.Amount));
            }
        }

        if (rootSave.UnlockedDoorIds != null)
        {
            for (int i = 0; i < rootSave.UnlockedDoorIds.Count; i++)
            {
                _unlockedDoorIds.Add(rootSave.UnlockedDoorIds[i]);
            }
        }

        HasCompletedIntroTutorial = rootSave.HasCompletedIntroTutorial;
    }

    public void ExportWorldStateToSave(SaveData rootSave)
    {
        if (rootSave == null) return;

        rootSave.OpenedChestIds = new List<string>(_openedChestIds);
        rootSave.ChestRemainingItems = new List<ChestRemainingItemSaveData>();

        foreach (var kvp in _chestRemainingMap)
        {
            List<ItemSlotSaveData> items = kvp.Value;
            if (items == null) continue;
            for (int i = 0; i < items.Count; i++)
            {
                rootSave.ChestRemainingItems.Add(new ChestRemainingItemSaveData(
                    kvp.Key, items[i].SlotIndex, items[i].ItemId, items[i].Amount));
            }
        }

        rootSave.UnlockedDoorIds = new List<string>(_unlockedDoorIds);
        rootSave.HasCompletedIntroTutorial = HasCompletedIntroTutorial;
    }

    public bool IsChestOpened(string chestId) => !string.IsNullOrEmpty(chestId) && _openedChestIds.Contains(chestId);

    public void SaveChestState(string chestId, IReadOnlyList<Item> runtimeItems)
    {
        if (string.IsNullOrEmpty(chestId)) return;

        _openedChestIds.Add(chestId);

        List<ItemSlotSaveData> serialized = new List<ItemSlotSaveData>();
        if (runtimeItems != null)
        {
            for (int i = 0; i < runtimeItems.Count; i++)
            {
                Item item = runtimeItems[i];
                if (item == null || item.Data == null) continue;
                int amount = (item is CountableItem countable) ? countable.CurrentAmount : 1;
                if (amount > 0)
                {
                    serialized.Add(new ItemSlotSaveData(i, item.Data.Id, amount));
                }
            }
        }
        _chestRemainingMap[chestId] = serialized;
    }

    public bool TryGetSavedChestItems(string chestId, out List<ItemSlotSaveData> savedItems)
    {
        if (!string.IsNullOrEmpty(chestId) && _chestRemainingMap.TryGetValue(chestId, out savedItems))
        {
            return true;
        }
        savedItems = null;
        return false;
    }

    public bool IsDoorUnlocked(string doorId) => !string.IsNullOrEmpty(doorId) && _unlockedDoorIds.Contains(doorId);
    public void MarkDoorUnlocked(string doorId)
    {
        if (!string.IsNullOrEmpty(doorId)) _unlockedDoorIds.Add(doorId);
    }

    public void MarkIntroTutorialCompleted() => HasCompletedIntroTutorial = true;

    public void SyncFromPlayer(PlayerController player)
    {
        if (player == null || player.StatusController == null) return;

        PlayerInfo info = player.StatusController.PlayerInfoSource;
        float curHp = player.StatusController.CurrentHp;

        CurrentSessionData = new PlayerSaveData(info, curHp);

        if (player.TryGetComponent(out PlayerInventoryHandler handler) && handler.InventoryInstance != null)
        {
            handler.InventoryInstance.ExportToSaveData(CurrentSessionData);
            CurrentSessionData.HasReceivedInitialEquipment = true;
        }
    }

    public void ClearSession()
    {
        CurrentSessionData = null;
        _openedChestIds.Clear();
        _chestRemainingMap.Clear();
        _unlockedDoorIds.Clear();
        HasCompletedIntroTutorial = false;
    }
}