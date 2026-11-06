using Cysharp.Threading.Tasks;

public enum WindowPausePolicy
{
    PauseTime,          // 시스템 메뉴 - 플레이어 조작 차단
    RealtimeCombat,     // 인게임 창 - 실시간 유지
}

/// <summary>
/// 인벤토리, 장비창, 메뉴 등 ESC 스택으로 닫히며 조작권을 점유하는 UI 추상 클래스
/// </summary>
public abstract class UIWindowView : UIBase
{
    [UnityEngine.Header("Window Policy")]
    [UnityEngine.SerializeField] protected WindowPausePolicy _pausePolicy;
    public WindowPausePolicy PausePolicy => _pausePolicy;
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
