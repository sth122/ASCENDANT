using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 플레이어의 인벤토리 데이터 및 퀵슬롯 입력을 전담 처리하는 독립 컴포넌트
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerInventoryHandler : MonoBehaviour
{
    [Header("Initial Loadout Configuration")]
    [SerializeField] private PlayerInitialEquipmentSO _initialEquipmentSO;

    private PlayerController _player;
    public Inventory InventoryInstance { get; private set; }

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        InventoryInstance = new Inventory();
    }

    private void Start()
    {
        CheckAndApplyInitialLoadout();
    }

    private void OnEnable()
    {
        if (_player != null && _player.InputReader != null)
        {
            _player.InputReader.OnQuickSlotDirectEvent -= HandleQuickSlotDirect;
            _player.InputReader.OnInventoryToggleEvent -= HandleInventoryToggle;
            _player.InputReader.OnQuickSlotDirectEvent += HandleQuickSlotDirect;
            _player.InputReader.OnInventoryToggleEvent += HandleInventoryToggle;
        }
    }

    private void OnDisable()
    {
        if (_player != null && _player.InputReader != null)
        {
            _player.InputReader.OnQuickSlotDirectEvent -= HandleQuickSlotDirect;
            _player.InputReader.OnInventoryToggleEvent -= HandleInventoryToggle;
        }
    }

    private void HandleQuickSlotDirect(int slotIndex)
    {
        if (!_player.IsControllerActive) return;

        IState curState = _player.StateMachineInstance.CurrentState;
        if (curState is PlayerIdleState || curState is PlayerMoveState)
        {
            InventoryInstance.QuickSlots.TryUseSlot(slotIndex, _player);
        }
    }
    private void HandleInventoryToggle()
    {
        if (UIManager.Instance == null) return;

        // 이미 열려 있다면 닫고, 닫혀 있다면 비동기 오픈
        InventoryModel model = new InventoryModel();
        UIManager.Instance.OpenUIAsync<InventoryView, InventoryPresenter, InventoryModel>(
            UIId.InventoryUI, model
        ).Forget();
    }

    private void CheckAndApplyInitialLoadout()
    {
        if (_initialEquipmentSO == null && DataManager.Instance != null)
        {
            DataManager.Instance.TryGetSingle<PlayerInitialEquipmentSO>(out _initialEquipmentSO);
        }

        SaveData save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSaveData : null;
        PlayerSaveData sessionData = SessionDataManager.Instance != null ? SessionDataManager.Instance.CurrentSessionData : null;

        // 1. 활성화된 세션에 장비 데이터가 있는 경우 (GameScene -> BossScene 전환)
        if (sessionData != null && sessionData.HasReceivedInitialEquipment && sessionData.EquippedWeaponId != 0)
        {
            RestoreEquipmentFromSession(sessionData);
            return;
        }

        // 세션 활성화 여부와 무관하게 세이브 데이터의 초기 지급 플래그를 검증
        bool isNewGame = (save == null) || (save.PlayerData != null && !save.PlayerData.HasReceivedInitialEquipment);
        if (isNewGame && _initialEquipmentSO != null)
        {
            InventoryInstance.ApplyInitialEquipmentLoadout(_initialEquipmentSO, _player);

            if (save != null && save.PlayerData != null) save.PlayerData.HasReceivedInitialEquipment = true;
            if (sessionData != null) sessionData.HasReceivedInitialEquipment = true;

            if (SaveManager.Instance != null && save != null)
            {
                SaveManager.Instance.SaveCurrentGameAsync(save.SaveSlotIndex).Forget();
            }
        }
    }
    private void RestoreEquipmentFromSession(PlayerSaveData session)
    {
        if (_initialEquipmentSO == null) return;

        // 세션에 저장된 장비 복원 (데이터 매니저 또는 SO 기반)
        InventoryInstance.ApplyInitialEquipmentLoadout(_initialEquipmentSO, _player);
        DebugLogger.LogWithTag("Inventory", "[세션 데이터 기반 장비 복원 완료]");
    }
}