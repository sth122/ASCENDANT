using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ConstStrings;

[Serializable]
public struct ChestRewardEntry
{
    public ItemData ItemData;
    [Min(1)] public int Amount;
}

/// <summary>
/// 보물 상자 상호작용 컴포넌트
/// 닫힌 상자(Chest) <-> 열린 상자(OpenChest) 모델 스왑, 월드 데이터 영속화, ChestUI 오픈을 담당합니다.
/// </summary>
public class ChestInteractable : BaseInteractable
{
    [Header("Chest Persistence ID (씬 내 고유 ID 필수)")]
    [SerializeField] private string _chestId = "GameScene_Chest_01";

    [Header("Visual Models (닫힌 상자 / 열린 상자 / 마법 방어막)")]
    [SerializeField] private GameObject _closedVisual;
    [SerializeField] private GameObject _openVisual;
    [SerializeField] private GameObject _sealedShieldVfx;

    [Header("Chest Contents (상자 보상 아이템 목록)")]
    [SerializeField] private List<ChestRewardEntry> _initialRewards = new List<ChestRewardEntry>();

    private readonly List<Item> _chestItems = new List<Item>();
    private bool _isOpened = false;
    private bool _isInitialized = false;

    public string ChestId => _chestId;
    public IReadOnlyList<Item> ChestItems => _chestItems;
    public bool HasRemainingItems => _chestItems.Count > 0;

    public override string InteractionPrompt
    {
        get
        {
            if (!_isOpened) return ConstString.OpenChest;
            return HasRemainingItems ? ConstString.ReopenChest : string.Empty;
        }
    }

    private void Awake()
    {
        _interactType = InteractType.Chest;
        EventBusSystem.Subscribe<GameSceneReadyEvent>(HandleSceneReady);
    }

    private void Start()
    {
        InitializeChestState();
    }

    private void OnDestroy()
    {
        EventBusSystem.Unsubscribe<GameSceneReadyEvent>(HandleSceneReady);
    }

    private void HandleSceneReady(GameSceneReadyEvent evt)
    {
        InitializeChestState();
    }

    public void InitializeChestState()
    {
        _chestItems.Clear();

        if (SessionDataManager.Instance.IsChestOpened(_chestId))
        {
            ApplyVisualState(isOpened: true);

            // 이미 개봉된 상자라면 세이브된 잔여 아이템만 복원
            if (SessionDataManager.Instance.TryGetSavedChestItems(_chestId, out List<ItemSlotSaveData> savedItems) &&
                DataManager.Instance != null)
            {
                for (int i = 0; i < savedItems.Count; i++)
                {
                    ItemSlotSaveData entry = savedItems[i];
                    if (DataManager.Instance.TryGetItemDataById(entry.ItemId, out ItemData itemData))
                    {
                        Item restored = itemData.CreateItem();
                        if (restored is CountableItem countable)
                        {
                            countable.SetAmount(entry.Amount);
                        }
                        _chestItems.Add(restored);
                    }
                }
            }
        }
        else
        {
            ApplyVisualState(isOpened: false);

            for (int i = 0; i < _initialRewards.Count; i++)
            {
                ChestRewardEntry reward = _initialRewards[i];
                if (reward.ItemData == null || reward.Amount <= 0) continue;

                Item item = reward.ItemData.CreateItem();
                if (item is CountableItem countable)
                {
                    countable.SetAmount(reward.Amount);
                }
                _chestItems.Add(item);
            }
        }

        _isInitialized = true;
    }

    public override void OnInteract(PlayerController player)
    {
        if (player == null) return;
        if (!_isInitialized) InitializeChestState();

        // 이미 개봉되었고 남은 아이템도 없다면 무시
        if (_isOpened && !HasRemainingItems) return;

        // 1. 처음 여는 순간 닫힌 상자 -> 열린 상자 모델 전환 및 월드 기록 저장
        if (!_isOpened)
        {
            ApplyVisualState(isOpened: true);
            PersistChestProgress(player);
        }

        // 2. 상자 UI(ChestUI) 오픈
        OpenChestUIAsync(player).Forget();
    }

    private async UniTaskVoid OpenChestUIAsync(PlayerController player)
    {
        if (UIManager.Instance == null) return;

        ChestModel model = new ChestModel(this, player);
        await UIManager.Instance.OpenUIAsync<ChestView, ChestPresenter, ChestModel>(
            UIId.ChestUI, model, this.destroyCancellationToken
        );
    }

    /// <summary>
    /// ChestPresenter에서 아이템 획득 시 호출하여 상자 내부 목록에서 제거하고 즉시 세이브 반영
    /// </summary>
    public void RemoveItemAt(int index, PlayerController player)
    {
        if (index < 0 || index >= _chestItems.Count) return;

        _chestItems.RemoveAt(index);
        PersistChestProgress(player);

        // 모든 아이템을 가져갔다면 상호작용 프롬프트 즉시 숨김
        if (!HasRemainingItems && UIManager.Instance != null &&
            UIManager.Instance.TryGetActiveView<PlayerHUDView>(UIId.PlayerHUD, out PlayerHUDView hudView))
        {
            hudView.HideInteractionPrompt();
        }
    }

    private void PersistChestProgress(PlayerController player)
    {
        SessionDataManager.Instance.SaveChestState(_chestId, _chestItems);

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveWorldInteractionProgress(player);
        }
    }

    private void ApplyVisualState(bool isOpened)
    {
        _isOpened = isOpened;

        if (_closedVisual != null) _closedVisual.SetActive(!isOpened);
        if (_openVisual != null) _openVisual.SetActive(isOpened);
        if (_sealedShieldVfx != null) _sealedShieldVfx.SetActive(!isOpened);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_chestId))
        {
            _chestId = $"{gameObject.scene.name}_{gameObject.name}_{transform.GetSiblingIndex()}";
        }
    }
#endif
}