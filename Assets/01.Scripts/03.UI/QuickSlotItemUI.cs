/// <summary>
/// 단일 퀵슬롯의 아이콘, 수량, 쿨타임 레이디얼 게이지, 선택 하이라이트 제어
/// </summary>
public class QuickSlotItemUI : UnityEngine.MonoBehaviour
{
    [UnityEngine.SerializeField] private UnityEngine.UI.Image _iconImage;
    [UnityEngine.SerializeField] private TMPro.TextMeshProUGUI _amountText;
    [UnityEngine.SerializeField] private UnityEngine.UI.Image _cooldownRadialImage;
    [UnityEngine.SerializeField] private UnityEngine.GameObject _selectionOutline;

    public void UpdateSlotUI(UnityEngine.Sprite icon, int amount, float cooldownRatio, bool isSelected)
    {
        if (icon != null && amount > 0)
        {
            _iconImage.gameObject.SetActive(true);
            _iconImage.sprite = icon;
            _amountText.gameObject.SetActive(amount > 1);
            _amountText.text = amount.ToString();
        }
        else
        {
            _iconImage.gameObject.SetActive(false);
            _amountText.gameObject.SetActive(false);
        }

        if (_cooldownRadialImage != null)
        {
            _cooldownRadialImage.fillAmount = UnityEngine.Mathf.Clamp01(cooldownRatio);
        }

        if (_selectionOutline != null)
        {
            _selectionOutline.SetActive(isSelected);
        }
    }
}