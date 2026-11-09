/// <summary>
/// 확인 및 취소 양방향 선택을 비동기로 대기하는 범용 모달 팝업
/// </summary>
public class ConfirmPopupView : UIPopupView<bool>
{
    [UnityEngine.Header("Popup UI Components")]
    [UnityEngine.SerializeField] private TMPro.TextMeshProUGUI _messageText;
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _confirmButton;
    [UnityEngine.SerializeField] private UnityEngine.UI.Button _cancelButton;

    protected override void Awake()
    {
        base.Awake();

        if (_confirmButton != null)
        {
            _confirmButton.onClick.AddListener(HandleConfirmClicked);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.AddListener(HandleCancelClicked);
        }
    }

    public void SetMessage(string message)
    {
        if (_messageText != null)
        {
            _messageText.text = message;
        }
    }

    public override void CancelPopup() => SetResult(false);

    private void HandleConfirmClicked() => SetResult(true);

    private void HandleCancelClicked() => SetResult(false);

    protected override void OnDestroy()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(HandleConfirmClicked);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveListener(HandleCancelClicked);
        }

        base.OnDestroy();
    }
}