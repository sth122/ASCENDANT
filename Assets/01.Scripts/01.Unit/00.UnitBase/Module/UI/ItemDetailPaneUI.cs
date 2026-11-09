using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemDetailPaneUI : MonoBehaviour
{
    [Header("Header Info")]
    [SerializeField] private Image _itemIcon;
    [SerializeField] private TextMeshProUGUI _itemNameText;
    [SerializeField] private TextMeshProUGUI _categoryText;

    [Header("Possession Counts")]
    [SerializeField] private TextMeshProUGUI _possessionCountText; // 소지수 / -
    [SerializeField] private TextMeshProUGUI _storageCountText;    // 저장수 / -

    [Header("Effect & Stats")]
    [SerializeField] private TextMeshProUGUI _effectSummaryText;
    [SerializeField] private TextMeshProUGUI _strScalingText;
    [SerializeField] private TextMeshProUGUI _dexScalingText;

    [Header("Description")]
    [SerializeField] private TextMeshProUGUI _descriptionText;

    public void ShowDetails(Item item)
    {
        if (item == null || item.Data == null)
        {
            ClearDetails();
            return;
        }

        ItemData data = item.Data;
        gameObject.SetActive(true);

        if (_itemIcon != null)
        {
            _itemIcon.gameObject.SetActive(true);
            _itemIcon.sprite = data.Icon;
        }

        if (_itemNameText != null) _itemNameText.text = data.ItemName;
        if (_categoryText != null) _categoryText.text = data.Category.ToString();

        // 소지수 및 저장고 한도
        int currentCount = item is CountableItem countable ? countable.CurrentAmount : 1;
        if (_possessionCountText != null) _possessionCountText.text = $"{currentCount} / {GetMaxStackString(data)}";
        if (_storageCountText != null) _storageCountText.text = $"- / {data.MaxStorageCount}";

        // 효과 요약 및 능력 보정
        if (_effectSummaryText != null) _effectSummaryText.text = data.GetEffectSummaryText();
        if (_strScalingText != null) _strScalingText.text = $"근력: {GetGradeString(data.StrScalingGrade)}";
        if (_dexScalingText != null) _dexScalingText.text = $"기량: {GetGradeString(data.DexScalingGrade)}";

        // 하단 설명
        if (_descriptionText != null) _descriptionText.text = data.Description;
    }

    public void ClearDetails()
    {
        if (_itemIcon != null) _itemIcon.gameObject.SetActive(false);
        if (_itemNameText != null) _itemNameText.text = "아이템을 선택하십시오";
        if (_categoryText != null) _categoryText.text = string.Empty;
        if (_possessionCountText != null) _possessionCountText.text = "- / -";
        if (_storageCountText != null) _storageCountText.text = "- / -";
        if (_effectSummaryText != null) _effectSummaryText.text = string.Empty;
        if (_strScalingText != null) _strScalingText.text = "-";
        if (_dexScalingText != null) _dexScalingText.text = "-";
        if (_descriptionText != null) _descriptionText.text = string.Empty;
    }

    private string GetMaxStackString(ItemData data)
    {
        return data is CountableItemData c ? c.MaxStack.ToString() : "1";
    }

    private string GetGradeString(StatScalingGrade grade)
    {
        return grade == StatScalingGrade.None ? "-" : grade.ToString();
    }
}