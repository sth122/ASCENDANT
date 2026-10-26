/// <summary>
/// ESC 입력 시 표출되는 인게임 시스템 메뉴 창 (UIWindowView)
/// 게임 재개, 타이틀 복귀, 게임 종료 버튼을 제공
/// </summary>
public class SystemMenuView : UIWindowView
{
    [UnityEngine.Header("System Menu Action Buttons")]
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _resumeButton;
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _titleButton;
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _quitButton;

    public UnityEngine.UI.Button ResumeButton => _resumeButton;
    public UnityEngine.UI.Button TitleButton => _titleButton;
    public UnityEngine.UI.Button QuitButton => _quitButton;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.Window;
        _pauseTimeWhenOpened = true;
    }
}