/// <summary>
/// 확인/취소 모달 팝업의 상태 데이터 모델
/// </summary>
public class ConfirmPopupModel : IUIModel
{
    public string Message { get; set; } = string.Empty;
    public string ConfirmButtonText { get; set; } = "확인";
    public string CancelButtonText { get; set; } = "취소";

    public ConfirmPopupModel() { }

    public ConfirmPopupModel(string message, string confirmText = "확인", string cancelText = "취소")
    {
        Message = message;
        ConfirmButtonText = confirmText;
        CancelButtonText = cancelText;
    }

    public void Reset()
    {
        Message = string.Empty;
        ConfirmButtonText = "확인";
        CancelButtonText = "취소";
    }
}