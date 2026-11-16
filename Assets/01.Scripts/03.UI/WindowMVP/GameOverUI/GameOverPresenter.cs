using Cysharp.Threading.Tasks;
using Util = DebugLogger<GameOverPresenter>;

/// <summary>
/// GameOverView와 씬 전환/화톳불 리스폰 로직을 중계하는 MVP Presenter
/// </summary>
public class GameOverPresenter : UIPresenterBase<GameOverView, GameOverModel>
{
    protected override void OnBind()
    {
        if (View != null)
        {
            View.TitleButton.onClick.AddListener(HandleTitleClicked);
            View.RespawnButton.onClick.AddListener(HandleRespawnClicked);
        }
    }

    protected override void OnUnbind()
    {
        if (View != null)
        {
            View.TitleButton.onClick.RemoveListener(HandleTitleClicked);
            View.RespawnButton.onClick.RemoveListener(HandleRespawnClicked);
        }
    }

    /// <summary>
    /// 타이틀 화면으로 복귀: 임의 저장을 하지 않고 마지막 화톳불 세이브 상태를 유지한 채 씬 전환
    /// </summary>
    private void HandleTitleClicked()
    {
        Util.Log("[게임오버] 타이틀 화면 복귀 요청");
        UnityEngine.Time.timeScale = 1.0f;

        if (SceneLoadManager.Instance != null)
        {
            SceneLoadManager.Instance.LoadSceneFlowAsync(SceneId.TitleScene).Forget();
        }
    }

    /// <summary>
    /// 마지막 화톳불에서 재시작: 현재 GameScene을 다시 로드하여 세이브 데이터 위치에서 부활
    /// </summary>
    private void HandleRespawnClicked()
    {
        Util.Log("[게임오버] 마지막 화톳불 체크포인트 부활 요청");
        UnityEngine.Time.timeScale = 1.0f;

        if (SceneLoadManager.Instance != null)
        {
            SceneLoadManager.Instance.LoadSceneFlowAsync(SceneId.GameScene).Forget();
        }
    }
}