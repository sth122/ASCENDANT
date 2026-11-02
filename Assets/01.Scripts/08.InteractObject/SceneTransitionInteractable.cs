using Cysharp.Threading.Tasks;
using UnityEngine;
using static ConstStrings.ConstString;
/// <summary>
/// 플레이어가 닿으면 세션을 동기화하고 지정된 씬으로 전환하는 트리거(포탈)
/// </summary>
public class SceneTransitionInteractable : BaseInteractable
{
    [Header("Transition Settings")]
    [SerializeField] private SceneId _targetScene;
    private bool _isTransitioning = false;
    private string _confirmPromptMessage = "보스 방에 진입하겠습니까?";

    public override void OnInteract(PlayerController player)
    {
        if (_isTransitioning || player == null) return;
        ExecuteBossEntryFlowAsync(player).Forget();
    }

    private async UniTaskVoid ExecuteBossEntryFlowAsync(PlayerController player)
    {
        _isTransitioning = true;

        try
        {
            // 간소화된 OpenUIAsync 호출 (어드레서블 키 문자열 생략)
            ConfirmPopupModel model = new ConfirmPopupModel(_confirmPromptMessage);
            ConfirmPopupView popup = await UIManager.Instance.OpenUIAsync<ConfirmPopupView, ConfirmPopupPresenter, ConfirmPopupModel>(
                UIId.ConfirmPopup, model
            );

            if(popup == null) return;

            bool isConfirmed = await popup.WaitForResultAsync();
            if (!isConfirmed)
            {
                // 취소 시 조작권은 popup.WaitForResultAsync() 내부 finally에서 자동 복구됨
                return;
            }

            DebugLogger.LogWithTag("SceneTransition", $"[{_targetScene}] 진입 승인: 세션 동기화 및 씬 전환 가동");

            // 2. 현재 플레이어 상태 세션에 보존
            if (SessionDataManager.Instance != null)
            {
                SessionDataManager.Instance.SyncFromPlayer(player);
            }

            // 3. 페이드 아웃과 함께 씬 비동기 로딩
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
