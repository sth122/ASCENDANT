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

    public SaveData CurrentSaveData { get; private set; }

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();
        EnsureSaveDirectory();
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
            Util.Log($"세이브 디렉토리 생성 완료: {dirPath}");
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
    public SaveData CreateNewGameSave(int slotIndex = 0)
    {
        CurrentSaveData = new SaveData
        {
            SaveSlotIndex = slotIndex,
            SaveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            LastSavedScene = SceneId.GameScene,
            HasCheckpoint = false,
            LastBonfirePosition = new SerializableVector3(0f, 0.1f, 0f),
            LastBonfireRotationY = 0f,
            PlayerData = new PlayerSaveData()
        };

        // 세션 데이터 동시 초기화
        SessionDataManager.Instance.LoadSessionFromSave(CurrentSaveData.PlayerData);

        SaveCurrentGameAsync(slotIndex).Forget();
        Util.Log($"[새 게임 세이브 생성 완료] 슬롯: {slotIndex}");
        return CurrentSaveData;
    }

    /// <summary>
    /// 로컬 파일로부터 세이브 데이터를 역직렬화하여 로드
    /// </summary>
    public bool LoadGame(int slotIndex = 0)
    {
        string filePath = GetSaveFilePath(slotIndex);

        if (!System.IO.File.Exists(filePath))
        {
            Util.LogWarning($"세이브 파일을 찾을 수 없습니다: {filePath}");
            CurrentSaveData = null;
            return false;
        }

        try
        {
            string json = System.IO.File.ReadAllText(filePath);
            CurrentSaveData = JsonUtility.FromJson<SaveData>(json);

            // 로드된 데이터를 인메모리 세션에 즉시 안착
            if (CurrentSaveData != null && CurrentSaveData.PlayerData != null)
            {
                SessionDataManager.Instance.LoadSessionFromSave(CurrentSaveData.PlayerData);
            }
            Util.Log($"[세이브 로드 성공] 슬롯: {slotIndex}, 타임스탬프: {CurrentSaveData.SaveTimestamp}");
            return true;
        }
        catch (System.Exception ex)
        {
            Util.LogError($"세이브 데이터 역직렬화 실패: {ex.Message}");
            CurrentSaveData = null;
            return false;
        }
    }

    /// <summary>
    /// 임시 파일에 먼저 기록 후 교체하여 파일 손상 방지
    /// </summary>
    public async UniTask SaveCurrentGameAsync(int slotIndex = 0)
    {
        if (CurrentSaveData == null)
        {
            Util.LogError("저장할 CurrentSaveData가 Null입니다.");
            return;
        }

        try
        {
            CurrentSaveData.SaveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string json = JsonUtility.ToJson(CurrentSaveData, prettyPrint: true);

            string targetFilePath = GetSaveFilePath(slotIndex);
            string tempFilePath = targetFilePath + ".tmp";

            // 1. 임시 파일에 비동기 기록
            await System.IO.File.WriteAllTextAsync(tempFilePath, json);

            // 2. 기록 완료 후 원자적 교체
            if (System.IO.File.Exists(targetFilePath))
            {
                System.IO.File.Delete(targetFilePath);
            }
            System.IO.File.Move(tempFilePath, targetFilePath);

            Util.Log($"[원자적 세이브 파일 기록 완료] 경로: {targetFilePath}");
        }
        catch (Exception ex)
        {
            Util.LogError($"세이브 파일 쓰기 실패: {ex.Message}");
        }
    }


    /// <summary>
    /// 화톳불(세이브 터미널)과 상호작용 시 현재 플레이어 위치 및 스탯 동기화 저장
    /// </summary>
    public void UpdateCheckpoint(Vector3 bonfirePosition, float bonfireRotationY, SceneId currentScene, PlayerInfo status, float currentHp, int slotIndex = 0)
    {
        if (CurrentSaveData == null)
        {
            CurrentSaveData = new SaveData();
        }

        CurrentSaveData.HasCheckpoint = true;
        CurrentSaveData.LastSavedScene = currentScene;
        CurrentSaveData.LastBonfirePosition = SerializableVector3.FromVector3(bonfirePosition);
        CurrentSaveData.LastBonfireRotationY = bonfireRotationY;
        CurrentSaveData.PlayerData = new PlayerSaveData(status, currentHp);

        SaveCurrentGameAsync(slotIndex).Forget();
        Util.Log($"[체크포인트 갱신 완료] 위치: {bonfirePosition}, 씬: {currentScene}");
    }
}