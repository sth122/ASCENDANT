using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static ConstStrings.ConstString;

/// <summary>
/// 보스방 안개 벽 등 씬 전환 상호작용 컴포넌트 (다중 선택지 프롬프트 기반)
/// </summary>
public class SceneTransitionInteractable : BaseInteractable
{
    [Header("Transition Settings")]
    [SerializeField] private SceneId _targetScene;
    private bool _isTransitioning = false;

    protected override bool HasMultipleChoices => !_isTransitioning;

    private void Awake() => _interactType = InteractType.BossDoor;

    protected override void BuildInteractionChoices(PlayerController player, List<InteractionChoice> outChoices)
    {
        if (_isTransitioning) return;

        string enterLabel = string.IsNullOrEmpty(BossEntryPrompt) ? "안개 너머로 들어간다" : BossEntryPrompt;
        outChoices.Add(new InteractionChoice(enterLabel, () => ExecuteBossEntryFlowAsync(player).Forget()));
        outChoices.Add(new InteractionChoice("그만둔다", () => { }));
    }

    private async UniTaskVoid ExecuteBossEntryFlowAsync(PlayerController player)
    {
        if (_isTransitioning || player == null) return;
        _isTransitioning = true;

        try
        {
            DebugLogger.LogWithTag("SceneTransition", $"[{_targetScene}] 진입 승인: 세션 동기화 및 씬 전환 가동");

            // 1. 현재 플레이어 상태 세션에 보존
            if (SessionDataManager.Instance != null)
            {
                SessionDataManager.Instance.SyncFromPlayer(player);
            }

            // 2. 페이드 아웃과 함께 씬 비동기 로딩
            if (SceneLoadManager.Instance != null)
            {
                await SceneLoadManager.Instance.LoadSceneFlowAsync(_targetScene);
            }
        }
        finally
        {
            _isTransitioning = false;
        }
    }
}