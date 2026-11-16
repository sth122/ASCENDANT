using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다중 페이지 조작법/가이드 이미지 슬라이드 및 단일 안내를 출력하는 모달 팝업 뷰
/// </summary>
public class NoticeWindowView : UIWindowView
{
    [Header("Guide Image")]
    [SerializeField] private Image _guideImage;

    [Header("Navigation & Page Indicator")]
    [SerializeField] private TextMeshProUGUI _pageIndicatorText;
    [SerializeField] private Button _topCloseButton;
    [SerializeField] private Button _prevButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private TextMeshProUGUI _confirmButtonText;

    private UniTaskCompletionSource<bool> _closeTcs;

    public Button TopCloseButton => _topCloseButton;
    public Button PrevButton => _prevButton;
    public Button NextButton => _nextButton;
    public Button ConfirmButton => _confirmButton;

    public override void InitializeView()
    {
        base.InitializeView();
        Identifier = UIId.NoticeWindowUI;
        Layer = UILayer.Window;
        _pausePolicy = WindowPausePolicy.RealtimeCombat;
    }

    public void RenderPage(NoticeModel model)
    {
        if (model == null || model.TotalPages == 0) return;

        Sprite currentSprite = model.GetCurrentSprite();
        bool isMultiPage = model.TotalPages > 1;

        if (_guideImage != null)
        {
            _guideImage.sprite = currentSprite;
            _guideImage.enabled = currentSprite != null;
        }

        if (_pageIndicatorText != null)
        {
            _pageIndicatorText.gameObject.SetActive(isMultiPage);
            if (isMultiPage)
            {
                _pageIndicatorText.text = $"{model.CurrentPageIndex + 1} / {model.TotalPages}";
            }
        }

        if (_prevButton != null)
        {
            _prevButton.gameObject.SetActive(isMultiPage);
            _prevButton.interactable = model.HasPreviousPage;
        }

        if (_nextButton != null)
        {
            _nextButton.gameObject.SetActive(isMultiPage && model.HasNextPage);
        }

        if (_confirmButton != null)
        {
            _confirmButton.gameObject.SetActive(!model.HasNextPage);
        }

        if (_confirmButtonText != null)
        {
            _confirmButtonText.text = model.FinalConfirmLabel;
        }
    }

    public UniTask WaitForCloseAsync()
    {
        if (_closeTcs == null || _closeTcs.Task.Status != UniTaskStatus.Pending)
        {
            _closeTcs = new UniTaskCompletionSource<bool>();
        }
        return _closeTcs.Task;
    }

    public void NotifyConfirmedAndClose()
    {
        SoundManager.Instance?.PlaySFX(SoundId.UI_Confirm);
        CloseWindowAsync().Forget();
    }

    public override async UniTask CloseWindowAsync(float duration = 0.15f)
    {
        await base.CloseWindowAsync(duration);
        _closeTcs?.TrySetResult(true);
    }

    private void OnDisable()
    {
        _closeTcs?.TrySetResult(true);
    }
}