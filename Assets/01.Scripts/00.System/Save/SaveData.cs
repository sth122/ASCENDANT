using System.Collections.Generic;

[System.Serializable]
public struct ChestRemainingItemSaveData
{
    public string ChestId;
    public int SlotIndex;
    public int ItemId;
    public int Amount;

    public ChestRemainingItemSaveData(string chestId, int slotIndex, int itemId, int amount)
    {
        ChestId = chestId;
        SlotIndex = slotIndex;
        ItemId = itemId;
        Amount = amount;
    }
}

/// <summary>
/// 게임 진행도의 단일 세이브 슬롯을 나타내는 루트 세이브 데이터 구조체
/// </summary>
[System.Serializable]
public class SaveData
{
    [UnityEngine.Header("Save Metadata")]
    public int SaveSlotIndex = 0;
    public bool IsCharacterCreated = false;
    public string CharacterName = "잿빛 기사";
    public int PortraitIndex = 0;
    public string SaveTimestamp = string.Empty;
    public float TotalPlayTime = 0f;

    [UnityEngine.Header("Checkpoint Information")]
    public SceneId LastSavedScene = SceneId.GameScene;
    public BonfireId LastBonfireId = BonfireId.None;
    public bool HasCheckpoint = false;
    public SerializableVector3 LastBonfirePosition;
    public float LastBonfireRotationY;

    [UnityEngine.Header("World Persistence Flags")]
    public List<string> OpenedChestIds = new List<string>();
    public List<ChestRemainingItemSaveData> ChestRemainingItems = new List<ChestRemainingItemSaveData>();
    public List<string> UnlockedDoorIds = new List<string>();
    public bool HasCompletedIntroTutorial = false;

    [UnityEngine.Header("Player Data")]
    public PlayerSaveData PlayerData = new PlayerSaveData();

    public SaveData()
    {
        SaveTimestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        LastSavedScene = SceneId.GameScene;
        LastBonfireId = BonfireId.None;
        HasCheckpoint = false;
        LastBonfirePosition = new SerializableVector3(0f, 0f, 0f);
        LastBonfireRotationY = 0f;
        OpenedChestIds = new List<string>();
        ChestRemainingItems = new List<ChestRemainingItemSaveData>();
        UnlockedDoorIds = new List<string>();
        HasCompletedIntroTutorial = false;
        PlayerData = new PlayerSaveData();
    }
}