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
        Util.Log("[신규 인게임 세션 데이터 생성 완료]");
    }

    /// <summary>
    /// 세이브 파일 로드 시 영속 데이터로부터 세션 복원
    /// </summary>
    public void LoadSessionFromSave(PlayerSaveData saveData)
    {
        if (saveData == null) return;

        CurrentSessionData = saveData;
        Util.Log($"[세이브 데이터 기반 세션 복원 완료] HP: {saveData.CurrentHp}, 레벨: {saveData.SoulLevel}");
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
        Util.Log($"[플레이어 -> 세션 동기화 완료] 잔여 HP: {curHp}");
    }

    public void ClearSession()
    {
        CurrentSessionData = null;
        Util.Log("[인게임 세션 데이터 초기화]");
    }
}