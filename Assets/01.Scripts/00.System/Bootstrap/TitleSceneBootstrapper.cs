/// <summary>
/// Title Scene 전용 Bootstrapper
/// </summary>
public class TitleSceneBootstrapper : SceneBootstrapper
{
    protected override void RegisterSceneServices() { }
    protected override Cysharp.Threading.Tasks.UniTask PreloadSceneAssetsAsync() => Cysharp.Threading.Tasks.UniTask.CompletedTask;
    protected override Cysharp.Threading.Tasks.UniTask SetupSceneActorsAsync() => Cysharp.Threading.Tasks.UniTask.CompletedTask;

    protected override async Cysharp.Threading.Tasks.UniTask SetupSceneUIAsync()
    {
        TitleMenuModel model = new TitleMenuModel
        {
            CanContinueGame = false // 세이브 파일 검증 로직 연결 가능
        };

        // 타이틀 시작 메뉴 오픈
        await UIManager.Instance.OpenUIAsync<TitleMenuView, TitleMenuPresenter, TitleMenuModel>(
                    UIId.None, "TitleMenuUI", model, ct: this.destroyCancellationToken);
    }

    protected override void OnSceneReadyCompleted()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameState.Title);
        }
        UIManager.Instance.InputReader.EnableUIInput();
    }
}
