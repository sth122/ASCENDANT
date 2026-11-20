using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using Util = DebugLogger<SaveManager>;

/// <summary>
/// 로컬 JSON 직렬화를 통해 세이브 파일의 생성, 쓰기, 읽기를 전담하는 싱글톤 매니저
/// </summary>
public class SaveManager : MonoSingleton<SaveManager>
{
    private const string SaveFilePrefix = "SaveSlot_";
    private const string SaveFileExtension = ".json";

    public const int MaxSaveSlots = 3;
    public int ActiveSlotIndex { get; private set; } = 0;
    private bool _isSaving = false;
    private bool _hasPendingSave = false;
    private int _pendingSlotIndex = 0;
    public SaveData CurrentSaveData { get; private set; }

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();
        EnsureSaveDirectory();
    }

    public void SetActiveSlot(int slotIndex) => ActiveSlotIndex = Mathf.Clamp(slotIndex, 0, MaxSaveSlots - 1);

    /// <summary>
    /// 캐릭터 선택 창(0~2번 슬롯) 표시용 세이브 요약 조회 (현재 활성 슬롯을 덮어쓰지 않음)
    /// </summary>
    public SaveData PeekSlotData(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);
        if (!System.IO.File.Exists(filePath)) return null;

        try
        {
            string json = System.IO.File.ReadAllText(filePath);
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch
        {
            return null;
        }
    }

    private string GetSaveDirectoryPath()
    {
        return System.IO.Path.Combine(Application.persistentDataPath, "Saves");
    }

    private string GetSaveFilePath(int slotIndex)
    {
        return System.IO.Path.Combine(GetSaveDirectoryPath(), $"{SaveFilePrefix}{slotIndex}{SaveFileExtension}");
    }

    private void EnsureSaveDirectory()
    {
        string dirPath = GetSaveDirectoryPath();
        if (!System.IO.Directory.Exists(dirPath))
        {
            System.IO.Directory.CreateDirectory(dirPath);
        }
    }

    /// <summary>
    /// 특정 슬롯에 유효한 세이브 파일이 존재하는지 검증
    /// </summary>
    public bool HasSaveData(int slotIndex = 0)
    {
        string filePath = GetSaveFilePath(slotIndex);
        return System.IO.File.Exists(filePath);
    }

    /// <summary>
    /// 새 게임을 위한 기본 세이브 데이터 생성 및 저장
    /// </summary>
    public SaveData CreateNewGameSave(int slotIndex, string characterName = "잿빛 기사", int portraitIndex = 0)
    {
        SetActiveSlot(slotIndex);

        CurrentSaveData = new SaveData
        {
            SaveSlotIndex = slotIndex,
            IsCharacterCreated = true,
            CharacterName = characterName,
            PortraitIndex = portraitIndex,
            SaveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            LastSavedScene = SceneId.GameScene,
            LastBonfireId = BonfireId.None,
            HasCheckpoint = false,
            LastBonfirePosition = new SerializableVector3(0f, 0.1f, 0f),
            LastBonfireRotationY = 0f,
            PlayerData = new PlayerSaveData()
        };


        SessionDataManager.Instance.LoadWorldStateFromSave(CurrentSaveData);
        SessionDataManager.Instance.LoadSessionFromSave(CurrentSaveData.PlayerData);

        SaveCurrentGameAsync(slotIndex).Forget();
        return CurrentSaveData;
    }

    /// <summary>
    /// 로컬 파일로부터 세이브 데이터를 역직렬화하여 로드
    /// </summary>
    public bool LoadGame(int slotIndex = 0)
    {
        SetActiveSlot(slotIndex);

        string filePath = GetSaveFilePath(slotIndex);
        if (!System.IO.File.Exists(filePath))
        {
            CurrentSaveData = null;
            SessionDataManager.Instance.ClearSession();
            return false;
        }

        try
        {
            string json = System.IO.File.ReadAllText(filePath);
            CurrentSaveData = JsonUtility.FromJson<SaveData>(json);

            if (CurrentSaveData != null && SessionDataManager.Instance != null)
            {
                SessionDataManager.Instance.ClearSession();
                SessionDataManager.Instance.LoadWorldStateFromSave(CurrentSaveData);
                if (CurrentSaveData.PlayerData != null)
                {
                    SessionDataManager.Instance.LoadSessionFromSave(CurrentSaveData.PlayerData);
                }
            }
            Util.Log($"[세이브 로드 성공] 슬롯: {slotIndex}, 타임스탬프: {CurrentSaveData.SaveTimestamp}");
            return true;
        }
        catch (System.Exception ex)
        {
            Util.LogError($"세이브 데이터 역직렬화 실패: {ex.Message}");
            CurrentSaveData = null;
            SessionDataManager.Instance?.ClearSession();
            return false;
        }
    }

    /// <summary>
    /// 임시 파일에 먼저 기록 후 교체하여 파일 손상 방지
    /// </summary>
    public async UniTask SaveCurrentGameAsync(int slotIndex = 0)
    {
        if (CurrentSaveData == null) return;

        if (_isSaving)
        {
            _hasPendingSave = true;
            _pendingSlotIndex = slotIndex;
            return;
        }

        _isSaving = true;
        int targetSlot = slotIndex;

        try
        {
            while (true)
            {
                _hasPendingSave = false;
                if (CurrentSaveData == null) break;

                EventBusSystem.Publish(new GameSavingEvent(targetSlot));

                CurrentSaveData.SaveSlotIndex = targetSlot;
                CurrentSaveData.SaveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string json = JsonUtility.ToJson(CurrentSaveData, prettyPrint: true);

                string targetFilePath = GetSaveFilePath(targetSlot);
                string tempFilePath = targetFilePath + ".tmp";

                await System.IO.File.WriteAllTextAsync(tempFilePath, json);

                // 파일 기록 도중 해당 슬롯이 삭제되었다면 임시 파일만 정리하고 중단
                if (CurrentSaveData == null || CurrentSaveData.SaveSlotIndex != targetSlot)
                {
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                    break;
                }

                if (System.IO.File.Exists(targetFilePath))
                {
                    System.IO.File.Delete(targetFilePath);
                }
                System.IO.File.Move(tempFilePath, targetFilePath);

                EventBusSystem.Publish(new GameSavedEvent(targetSlot));
                Util.Log($"[원자적 세이브 파일 기록 완료] 경로: {targetFilePath}");

                if (!_hasPendingSave) break;
                targetSlot = _pendingSlotIndex;
            }
        }
        catch (Exception ex)
        {
            Util.LogError($"세이브 파일 쓰기 실패: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }


    /// <summary>
    /// 화톳불(세이브 터미널)과 상호작용 시 현재 플레이어 위치 및 스탯 동기화 저장
    /// </summary>
    public void UpdateCheckpoint(BonfireId bonfireId, Vector3 bonfirePosition, float bonfireRotationY, SceneId currentScene, PlayerController player)
    {
        if (CurrentSaveData == null) CurrentSaveData = new SaveData();

        CurrentSaveData.SaveSlotIndex = ActiveSlotIndex;
        CurrentSaveData.IsCharacterCreated = true;
        CurrentSaveData.HasCheckpoint = true;
        CurrentSaveData.LastBonfireId = bonfireId;
        CurrentSaveData.LastSavedScene = currentScene;
        CurrentSaveData.LastBonfirePosition = SerializableVector3.FromVector3(bonfirePosition);
        CurrentSaveData.LastBonfireRotationY = bonfireRotationY;

        SessionDataManager.Instance.ExportWorldStateToSave(CurrentSaveData);

        PlayerInfo status = player.StatusController.PlayerInfoSource;
        CurrentSaveData.PlayerData = new PlayerSaveData(status, player.StatusController.CurrentHp);

        if (player.TryGetComponent(out PlayerInventoryHandler handler) && handler.InventoryInstance != null)
        {
            handler.InventoryInstance.ExportToSaveData(CurrentSaveData.PlayerData);
            CurrentSaveData.PlayerData.HasReceivedInitialEquipment = true;
        }

        SaveCurrentGameAsync(ActiveSlotIndex).Forget();
    }

    /// <summary>
    /// 보물상자 획득 또는 열쇠 문 개방 시 플레이어 인벤토리와 월드 진행도를 즉시 세이브 파일에 기록
    /// </summary>
    public void SaveWorldInteractionProgress(PlayerController player)
    {
        if (CurrentSaveData == null)
        {
            CurrentSaveData = new SaveData
            {
                SaveSlotIndex = ActiveSlotIndex,
                IsCharacterCreated = true
            };
        }

        SessionDataManager.Instance.ExportWorldStateToSave(CurrentSaveData);

        if (player != null && player.StatusController != null)
        {
            PlayerInfo status = player.StatusController.PlayerInfoSource;
            CurrentSaveData.PlayerData = new PlayerSaveData(status, player.StatusController.CurrentHp);

            if (player.TryGetComponent(out PlayerInventoryHandler handler) && handler.InventoryInstance != null)
            {
                handler.InventoryInstance.ExportToSaveData(CurrentSaveData.PlayerData);
                CurrentSaveData.PlayerData.HasReceivedInitialEquipment = true;
            }
        }

        SaveCurrentGameAsync(ActiveSlotIndex).Forget();
    }

    /// <summary>
    /// 특정 슬롯의 세이브 파일을 영구 삭제
    /// </summary>
    public bool DeleteSaveData(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);
        string tempFilePath = filePath + ".tmp";

        try
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
            if (System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
            }

            if (CurrentSaveData != null && CurrentSaveData.SaveSlotIndex == slotIndex)
            {
                CurrentSaveData = null;
                _hasPendingSave = false;
                SessionDataManager.Instance?.ClearSession();
            }
            return true;
        }
        catch (Exception ex)
        {
            Util.LogError($"세이브 슬롯 삭제 실패 (슬롯 {slotIndex}): {ex.Message}");
            return false;
        }
    }
}