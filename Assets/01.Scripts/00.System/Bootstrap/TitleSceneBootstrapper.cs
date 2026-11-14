using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// TitleScene 전용 부트스트래퍼
/// 전역 매니저 초기화 + 타이틀 배경/메뉴 사전 준비 + 로고 인트로 시퀀스를 총괄
/// </summary>
public class TitleSceneBootstrapper : SceneBootstrapper
{
    [UnityEngine.SerializeField] private TitleIntroView _titleIntroView;
    private TitleMenuView _titleMenuView;

    protected override void Awake()
    {
        SettingModel.ApplySavedSettingsOnStartup();
        base.Awake();
    }

    protected override void RegisterSceneServices() { }
    protected override async UniTask PreloadSceneAssetsAsync()
    {
        CancellationToken ct = this.destroyCancellationToken;

        // 중앙 로고 페이드 인과 백그라운드 UI(TitleBackgroundUI, TitleMenuUI) 로드를 병렬 실행
        UniTask logoTask = _titleIntroView != null ? _titleIntroView.FadeInCenterLogoAsync(1.2f, ct) : UniTask.CompletedTask;

        await UniTask.WhenAll(logoTask, PrepareTitleBackgroundAsync(ct), PrepareTitleMenuAsync(ct));
    }

    private async UniTask PrepareTitleBackgroundAsync(CancellationToken ct)
    {
        if (UIManager.Instance == null) return;

        await UIManager.Instance.OpenUIAsync<TitleBackgroundView, TitleBackgroundPresenter, TitleBackgroundModel>(
            UIId.TitleBackgroundUI, ct: ct);
    }

    private async UniTask PrepareTitleMenuAsync(CancellationToken ct)
    {
        if (UIManager.Instance == null) return;

        _titleMenuView = await UIManager.Instance.OpenUIAsync<TitleMenuView, TitleMenuPresenter, TitleMenuModel>(
            UIId.TitleMenuUI, new TitleMenuModel(), ct: ct);

        if (_titleMenuView != null)
        {
            await _titleMenuView.HideAsync(0f, ct);
        }
    }

    protected override UniTask SetupSceneActorsAsync() =>  UniTask.CompletedTask;

    protected override async  UniTask SetupSceneUIAsync()
    {
        CancellationToken ct = this.destroyCancellationToken;

        // 타이틀 BGM 재생 시작
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(SoundId.BGM_Title, fadeDuration: 1.5f);
        }

        if (_titleIntroView != null)
        {
            // 2. 모든 데이터/풀/UI 초기화가 완료된 상태에서 Press Any Button 대기
            await _titleIntroView.WaitForAnyButtonAsync(ct);

            // 3. 로고 상단 이동 및 암막 해제
            await _titleIntroView.TransitionLogoToHeaderAsync(0.85f, ct);
        }

        // 4. 타이틀 메뉴 버튼(새 게임, 이어하기, 설정, 종료) 페이드 인 등장
        if (_titleMenuView != null)
        {
            await _titleMenuView.ShowAsync(0.35f, ct);
        }
    }

    protected override void OnSceneReadyCompleted()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameState.Title);
        }
        InputSystemHandler.SetMode(InputMode.Title);
    }
}