using Cysharp.Threading.Tasks;

/// <summary>
/// 타이틀 화면 시각 요소 뷰 (버튼, 로고 등)
/// </summary>
public class TitleMenuView : UIWindowView
{
    [UnityEngine.Header("Title Buttons")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _startButton;
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _settingsButton;
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _quitButton;

    public UnityEngine.UI.Button StartButton => _startButton;
    public UnityEngine.UI.Button SettingsButton => _settingsButton;
    public UnityEngine.UI.Button QuitButton => _quitButton;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.Window;
    }

    public override async UniTask CloseWindowAsync(float duration = 0.15f)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Title)
        {
            return;
        }
        await base.CloseWindowAsync();
    }
}