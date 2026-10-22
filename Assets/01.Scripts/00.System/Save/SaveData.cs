/// <summary>
/// 게임 진행도의 단일 세이브 슬롯을 나타내는 루트 세이브 데이터 구조체
/// </summary>
[System.Serializable]
public class SaveData
{
    [UnityEngine.Header("Save Metadata")]
    public int SaveSlotIndex = 0;
    public string SaveTimestamp = string.Empty;
    public float TotalPlayTime = 0f;

    [UnityEngine.Header("Checkpoint Information")]
    public SceneId LastSavedScene = SceneId.GameScene;
    public SerializableVector3 LastBonfirePosition;
    public float LastBonfireRotationY;

    [UnityEngine.Header("Player Data")]
    public PlayerSaveData PlayerData = new PlayerSaveData();

    public SaveData()
    {
        SaveTimestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        LastSavedScene = SceneId.GameScene;
        LastBonfirePosition = new SerializableVector3(0f, 0f, 0f);
        LastBonfireRotationY = 0f;
        PlayerData = new PlayerSaveData();
    }
}