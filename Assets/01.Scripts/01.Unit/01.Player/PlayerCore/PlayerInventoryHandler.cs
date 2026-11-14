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

    private void OnEnable()
    {
        if (_player != null && _player.InputReader != null)
        {
            _player.InputReader.OnQuickSlotDirectEvent -= HandleQuickSlotDirect;
            _player.InputReader.OnQuickSlotDirectEvent += HandleQuickSlotDirect;
        }
    }

    private void OnDisable()
    {
        if (_player != null && _player.InputReader != null)
        {
            _player.InputReader.OnQuickSlotDirectEvent -= HandleQuickSlotDirect;
        }
    }

    /// <summary>
    /// 부트스트래퍼에서 PlayerController 스탯 세팅이 완료된 직후 안전하게 호출
    /// </summary>
    public void InitializeInventory(DataManager dataManager)
    {
        SaveData save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSaveData : null;
        PlayerSaveData sessionData = SessionDataManager.Instance != null && SessionDataManager.Instance.HasActiveSession
            ? SessionDataManager.Instance.CurrentSessionData : null;

        // 1. 활성화된 세션 데이터가 있는 경우 (GameScene -> BossScene 씬 전환 복원)
        if (sessionData != null && sessionData.HasReceivedInitialEquipment)
        {
            RestoreFromSaveData(sessionData, dataManager);
            EnsureMandatoryWeaponAndShield();
            return;
        }

        // 2. 화톳불 영속 세이브에 인벤토리 스냅샷이 있는 경우 (게임 이어하기)
        if (save != null && save.PlayerData != null && save.PlayerData.HasReceivedInitialEquipment)
        {
            RestoreFromSaveData(save.PlayerData, dataManager);
            EnsureMandatoryWeaponAndShield();
            return;
        }

        // 3. 신규 게임 초기 로드아웃 지급
        ApplyInitialLoadout(save, sessionData);
    }

    /// <summary>
    /// 구 세이브 데이터 로드 시에도 필수 무기/방패가 비어있지 않도록 보장
    /// </summary>
    private void EnsureMandatoryWeaponAndShield()
    {
        if (_initialEquipmentSO == null && DataManager.Instance != null)
        {
            DataManager.Instance.TryGetSingle<PlayerInitialEquipmentSO>(out _initialEquipmentSO);
        }
        if (_initialEquipmentSO == null || InventoryInstance == null) return;

        if (InventoryInstance.GetEquippedItem(EquipSlotType.MainWeapon) == null && _initialEquipmentSO.StartingWeapon != null)
        {
            InventoryInstance.EquipDirect(_initialEquipmentSO.StartingWeapon, _player);
        }
        if (InventoryInstance.GetEquippedItem(EquipSlotType.Shield) == null && _initialEquipmentSO.StartingShield != null)
        {
            InventoryInstance.EquipDirect(_initialEquipmentSO.StartingShield, _player);
        }
    }

    private void ApplyInitialLoadout(SaveData save, PlayerSaveData sessionData)
    {
        if (_initialEquipmentSO == null && DataManager.Instance != null)
        {
            DataManager.Instance.TryGetSingle<PlayerInitialEquipmentSO>(out _initialEquipmentSO);
        }

        if (_initialEquipmentSO == null)
        {
            DebugLogger.LogErrorWithTag("PlayerInventoryHandler", "PlayerInitialEquipmentSO를 찾을 수 없습니다.");
            return;
        }

        // 초기 장비, 방어구, 소모품 지급
        InventoryInstance.ApplyInitialEquipmentLoadout(_initialEquipmentSO, _player);

        if (save != null && save.PlayerData != null)
        {
            save.PlayerData.HasReceivedInitialEquipment = true;
        }
        if (sessionData != null)
        {
            sessionData.HasReceivedInitialEquipment = true;
        }
        if (SaveManager.Instance != null && save != null)
        {
            SaveManager.Instance.SaveCurrentGameAsync(save.SaveSlotIndex).Forget();
        }

        DebugLogger.LogWithTag("PlayerInventoryHandler", "[신규 게임 초기 장비 지급 완료]");
    }

    #region 세션/세이브 데이터 직렬화 및 복원 브릿지
    public void ExportToSaveData(PlayerSaveData targetData)
    {
        if (targetData == null || InventoryInstance == null) return;
        InventoryInstance.ExportToSaveData(targetData);
    }

    public void RestoreFromSaveData(PlayerSaveData sourceData, DataManager dataManager)
    {
        if (sourceData == null || InventoryInstance == null || _player == null) return;
        InventoryInstance.RestoreFromSaveData(sourceData, _player, dataManager);
        DebugLogger.LogWithTag("PlayerInventoryHandler", "[인벤토리 및 장착 상태 복원 완료]");
    }
    #endregion

    #region 입력 핸들러 및 FSM 연동
    private void HandleQuickSlotDirect(int slotIndex)
    {
        if (!_player.IsControllerActive || InventoryInstance == null) return;

        QuickSlotSystem quickSlots = InventoryInstance.QuickSlots;

        // 1. 아이템 사용 모션 중이거나 공용 쿨타임 중이면 상태 전환 자체를 차단
        if (quickSlots.IsUsingItemLock || quickSlots.IsOnGlobalCooldown) return;

        IState curState = _player.StateMachineInstance.CurrentState;
        if (curState is PlayerUseItemState) return;

        if (curState is PlayerIdleState || curState is PlayerMoveState || curState is PlayerSprintState)
        {
            ConsumableItem item = quickSlots.GetItem(slotIndex) as ConsumableItem;
            if (item == null || item.CurrentAmount <= 0) return;

            if (_player.StateMachineInstance.GetState(UnitState.UseItem) is PlayerUseItemState useItemState)
            {
                quickSlots.IsUsingItemLock = true;
                useItemState.SetupItemToUse(slotIndex, item);
                _player.StateMachineInstance.ChangeState(UnitState.UseItem);
            }
        }
    }
    #endregion
}