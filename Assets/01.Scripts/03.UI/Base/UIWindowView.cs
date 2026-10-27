using Cysharp.Threading.Tasks;

/// <summary>
/// 인벤토리, 장비창, 메뉴 등 ESC 스택으로 닫히며 조작권을 점유하는 UI 추상 클래스
/// </summary>
public abstract class UIWindowView : UIBase
{
    [UnityEngine.Header("Window Options")]
    [UnityEngine.SerializeField] protected bool _pauseTimeWhenOpened = false;
    public bool PauseTimeWhenOpened => _pauseTimeWhenOpened;
    private bool _isClosing = false;
    public virtual async UniTask CloseWindowAsync(float duration = 0.15f)
    {
        // 이미 닫히는 중이라면 중복 실행 무시 (토큰 취소 방지)
        if (_isClosing) return;
        _isClosing = true;

        try
        {
            await HideAsync(duration);

            if (UIManager.Instance != null)
            {
                UIManager.Instance.NotifyWindowClosed(this);
            }
        }
        finally
        {
            _isClosing = false;
        }
    }
}
