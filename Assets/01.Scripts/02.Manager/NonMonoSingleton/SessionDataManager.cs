/// <summary>
/// 씬 전환 간 플레이어의 런타임 상태(현재 체력, 소울, 인벤토리 등)를 일시 보존하는 인메모리 세션 매니저
/// </summary>
public class SessionDataManager : NonMonoSingleton<SessionDataManager>
{
    public PlayerSaveData CurrentSessionData { get; private set; }
    public bool HasActiveSession => CurrentSessionData != null;

    public void UpdateSession(PlayerInfo status, float currentHp)
    {
        CurrentSessionData = new PlayerSaveData(status, currentHp);
    }

    public void ClearSession()
    {
        CurrentSessionData = null;
    }
}