using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 소울라이크 화톳불 상호작용 컴포넌트 (휴식한다 / 레벨 업 / 떠난다 선택지 제공)
/// </summary>
public class BonfireInteractable : BaseInteractable, ISceneLoadable
{
    [SerializeField] private BonfireId _bonfireId = BonfireId.GameScene_Start;
    [SerializeField] private Transform _respawnPoint;
    [SerializeField] private SceneId _currentScene;
    private int _sceneLoadOrder = 15;

    public int SceneLoadOrder => _sceneLoadOrder;
    public BonfireId Id => _bonfireId;

    protected override bool HasMultipleChoices => true;

    private void Awake() => _interactType = InteractType.Bonfire;

    #region ISceneLoadable
    public UniTask OnSceneObjectLoadAsync(SceneId scene, CancellationToken ct)
    {
        Transform targetPoint = _respawnPoint != null ? _respawnPoint : transform;
        SavePointManager.Instance.RegisterSavePoint(_bonfireId, targetPoint);
        return UniTask.CompletedTask;
    }

    public void OnSceneObjectReady(SceneId scene) { }
    #endregion

    protected override void BuildInteractionChoices(PlayerController player, List<InteractionChoice> outChoices)
    {
        outChoices.Add(new InteractionChoice("휴식한다", () => ExecuteRest(player)));
        outChoices.Add(new InteractionChoice("레벨 업", () => OpenBonfireLevelUpUIAsync().Forget()));
        outChoices.Add(new InteractionChoice("떠난다", () => { }));
    }

    private void ExecuteRest(PlayerController player)
    {
        if (player == null) return;

        Transform spawnTr = _respawnPoint != null ? _respawnPoint : transform;

        // 1. 플레이어 상태 완전 회복
        player.StatusController.HealHp(player.StatusController.MaxHp);
        player.StatusController.ResetPoiseToDefault();
        if (player.StatusController is PlayerStatusController playerStat)
        {
            playerStat.RestoreStamina(playerStat.MaxStamina);
        }

        // 2. 세이브 매니저 영속 파일 기록 (저장 중.. -> 저장 완료 토스트 출력)
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.UpdateCheckpoint(_bonfireId, spawnTr.position, spawnTr.eulerAngles.y, _currentScene, player);
        }

        // 3. 휴식 SFX 재생 및 월드 리셋 이벤트 발행
        SoundManager.Instance?.PlaySFX(SoundId.Bonfire_Rest, transform.position);
        EventBusSystem.Publish(new BonfireRestedEvent(spawnTr.position));
    }

    private async UniTaskVoid OpenBonfireLevelUpUIAsync()
    {
        if (UIManager.Instance == null) return;
        await UIManager.Instance.OpenUIAsync<BonfireWindowView, BonfirePresenter, BonfireModel>(UIId.BonfireUI);
    }
}