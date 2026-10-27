/// <summary>
/// 타이틀 화면 시각 요소 뷰 (버튼, 로고 등)
/// </summary>
public class TitleMenuView : UIWindowView
{
    [UnityEngine.Header("Title Buttons")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _newGameButton;
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _continueButton;
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _settingsButton;
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _quitButton;

    public UnityEngine.UI.Button NewGameButton => _newGameButton;
    public UnityEngine.UI.Button ContinueButton => _continueButton;
    public UnityEngine.UI.Button SettingsButton => _settingsButton;
    public UnityEngine.UI.Button QuitButton => _quitButton;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.Window;
    }

    public void SetContinueInteractable(bool interactable)
    {
        if (_continueButton != null)
        {
            _continueButton.interactable = interactable;
        }
    }
}