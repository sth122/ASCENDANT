using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 새 게임 시작 후 GameScene 첫 진입 시 1회에 한해
/// 조작법/전투/상호작용 통합 가이드 슬라이드(UIId.NoticePopup)를 출력하는 매니저
/// </summary>
public class TutorialGuideManager : MonoBehaviour
{
    [Header("Tutorial Guide Pages SO")]
    [SerializeField] private TutorialDatabaseSO _tutorialDatabase;

    [Header("Display Options")]
    [SerializeField] private float _initialShowDelay = 0.35f;
    [SerializeField] private string _finalButtonLabel = "모험 시작";
    [Tooltip("에디터에서 타이틀을 거치지 않고 GameScene을 직접 실행할 때도 1회 테스트 출력할지 여부")]
    [SerializeField] private bool _showWithoutSaveInEditor = true;

    private bool _hasTriggeredThisLoad = false;

    private void Awake()
    {
        EventBusSystem.Subscribe<GameSceneReadyEvent>(HandleGameSceneReady);
    }

    private void OnDestroy()
    {
        EventBusSystem.Unsubscribe<GameSceneReadyEvent>(HandleGameSceneReady);
    }

    private void HandleGameSceneReady(GameSceneReadyEvent evt)
    {
        if (_hasTriggeredThisLoad) return;
        if (_tutorialDatabase == null || _tutorialDatabase.PageCount == 0) return;

        bool hasActiveSave = SaveManager.Instance != null && SaveManager.Instance.CurrentSaveData != null;

        if (hasActiveSave)
        {
            if (SessionDataManager.Instance.HasCompletedIntroTutorial) return;
        }
        else if (!_showWithoutSaveInEditor)
        {
            return;
        }

        _hasTriggeredThisLoad = true;
        SessionDataManager.Instance.MarkIntroTutorialCompleted();

        // 세이브 파일에도 1회 시청 완료 즉시 영속 기록
        if (hasActiveSave && SaveManager.Instance != null)
        {
            PlayerController player = null;
            if (ServiceLocator.TryGet<IPlayerProvider>(out var provider) && provider.IsPlayerSpawned)
            {
                player = provider.Player;
            }
            SaveManager.Instance.SaveWorldInteractionProgress(player);
        }

        ShowIntroTutorialAsync(this.destroyCancellationToken).Forget();
    }

    private async UniTaskVoid ShowIntroTutorialAsync(CancellationToken ct)
    {
        if (_initialShowDelay > 0f)
        {
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(_initialShowDelay), cancellationToken: ct).SuppressCancellationThrow();
            if (canceled) return;
        }

        if (UIManager.Instance == null) return;

        NoticeModel model = new NoticeModel(_tutorialDatabase.Pages, _finalButtonLabel);
        NoticeWindowView popup = await UIManager.Instance.OpenUIAsync<NoticeWindowView, NoticePresenter, NoticeModel>(
            UIId.NoticeWindowUI, model, ct
        );

        if (popup != null)
        {
            await popup.WaitForCloseAsync();
        }
    }
}