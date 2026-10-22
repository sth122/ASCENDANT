using Cysharp.Threading.Tasks;

public class TitleSceneBootstrapper : SceneBootstrapper
{
    protected override void RegisterSceneServices() { }
    protected override UniTask PreloadSceneAssetsAsync() => UniTask.CompletedTask;
    protected override UniTask SetupSceneActorsAsync() => UniTask.CompletedTask;

    protected override async UniTask SetupSceneUIAsync()
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
        UIManager.Instance.InputReader.EnableUIInput();
    }
}
