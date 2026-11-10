using Cysharp.Threading.Tasks;
using static ConstStrings.ConstString;
/// <summary>
/// 소울라이크 화톳불 상호작용 컴포넌트
/// </summary>
public class BonfireInteractable : BaseInteractable
{
    [UnityEngine.SerializeField] private UnityEngine.Transform _respawnPoint;
    [UnityEngine.SerializeField] private SceneId _currentScene;

    private void Awake() => _interactType = InteractType.Bonfire;

    public override void OnInteract(PlayerController player)
    {
        if (player == null) return;

        UnityEngine.Vector3 spawnPos = _respawnPoint != null ? _respawnPoint.position : transform.position;
        float rotY = _respawnPoint != null ? _respawnPoint.eulerAngles.y : transform.eulerAngles.y;

        // 1. 플레이어 상태 완전 회복
        player.StatusController.HealHp(player.StatusController.MaxHp);
        player.StatusController.ResetPoiseToDefault();
        if (player.StatusController is PlayerStatusController playerStat)
        {
            playerStat.RestoreStamina(playerStat.MaxStamina);
        }

        // 2. 세이브 매니저 영속 파일 기록
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.UpdateCheckpoint(spawnPos, rotY, _currentScene, player, slotIndex: 0);

            if (player.TryGetComponent(out PlayerInventoryHandler invHandler) && SaveManager.Instance.CurrentSaveData != null)
            {
                invHandler.ExportToSaveData(SaveManager.Instance.CurrentSaveData.PlayerData);
                SaveManager.Instance.SaveCurrentGameAsync(0).Forget();
            }
        }

        // 3. 월드 리셋 이벤트 발행 (EnemyManager 등이 수신하여 몬스터 리스폰)
        EventBusSystem.Publish(new BonfireRestedEvent(spawnPos));
        DebugLogger.LogWithTag("Bonfire", "화톳불 휴식 완료: 상태 회복, 세이브 갱신 및 월드 리셋");
    }
}