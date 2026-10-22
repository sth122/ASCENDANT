/// <summary>
/// 인벤토리, 장비창, 메뉴 등 ESC 스택으로 닫히며 조작권을 점유하는 UI 추상 클래스
/// </summary>
public abstract class UIWindowView : UIBase
{
    [UnityEngine.Header("Window Options")]
    [UnityEngine.SerializeField] private bool _pauseTimeWhenOpened = false;
    public bool PauseTimeWhenOpened => _pauseTimeWhenOpened;

    protected override void Awake()
    {
        base.Awake();
        Layer = UILayer.Window;
    }

    /// <summary>
    /// ESC 또는 취소 버튼 입력 시 호출되는 닫기 엔트리포인트
    /// </summary>
    public virtual async Cysharp.Threading.Tasks.UniTask CloseWindowAsync(System.Threading.CancellationToken ct = default)
    {
        await HideAsync(0.15f, ct);
        UIManager.Instance.NotifyWindowClosed(this);
    }
}
