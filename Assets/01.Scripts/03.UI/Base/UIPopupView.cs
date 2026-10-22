using Cysharp.Threading.Tasks;

/// <summary>
/// 호출 측에서 비동기로 유저의 선택(Result)을 대기할 수 있는 범용 모달 팝업 베이스
/// </summary>
public abstract class UIPopupView<TResult> : UIBase
{
    [UnityEngine.Header("Modal Dimmer")]
    [UnityEngine.SerializeField] protected UnityEngine.UI.Button _dimmedBackgroundButton;

    protected UniTaskCompletionSource<TResult> _popupTaskSource;

    protected override void Awake()
    {
        base.Awake();
        Layer = UILayer.Popup;

        if (_dimmedBackgroundButton != null)
        {
            _dimmedBackgroundButton.onClick.AddListener(HandleDimmedClicked);
        }
    }

    /// <summary>
    /// 팝업을 열고 유저가 결정을 내릴 때까지의 비동기 Task 반환
    /// </summary>
    public virtual async UniTask<TResult> WaitForResultAsync(System.Threading.CancellationToken ct = default)
    {
        _popupTaskSource = new UniTaskCompletionSource<TResult>();

        await ShowAsync(0.2f, ct);

        // 유저 클릭 또는 취소까지 대기
        TResult result = await _popupTaskSource.Task;

        await HideAsync(0.15f, ct);

        return result;
    }

    /// <summary>
    /// 팝업 결과를 확정짓고 대기를 해제하는 메서드
    /// </summary>
    protected void SetResult(TResult result)
    {
        if (_popupTaskSource != null && !_popupTaskSource.Task.Status.IsCompleted())
        {
            _popupTaskSource.TrySetResult(result);
        }
    }

    /// <summary>
    /// ESC 입력 시 기본 취소 결과 반환
    /// </summary>
    public abstract void CancelPopup();

    protected virtual void HandleDimmedClicked() => CancelPopup();

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_dimmedBackgroundButton != null)
        {
            _dimmedBackgroundButton.onClick.RemoveListener(HandleDimmedClicked);
        }
    }
}