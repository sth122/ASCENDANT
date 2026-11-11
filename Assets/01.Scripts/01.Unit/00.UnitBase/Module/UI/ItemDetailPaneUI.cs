using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemDetailPaneUI : MonoBehaviour
{
    [Header("Header Info (Common)")]
    [SerializeField] private Image _itemIcon;
    [SerializeField] private TextMeshProUGUI _itemNameText;
    [SerializeField] private TextMeshProUGUI _categoryText;

    [Header("Sub Panels")]
    [SerializeField] private GameObject _consumablePanel;
    [SerializeField] private GameObject _equipmentPanel;

    [Header("Consumable Sub-Panel (image_945d19.jpg)")]
    [SerializeField] private TextMeshProUGUI _possessionCountText;
    [SerializeField] private TextMeshProUGUI _storageCountText;
    [SerializeField] private TextMeshProUGUI _effectSummaryText;
    [SerializeField] private TextMeshProUGUI _consumableDescText;

    [Header("Equipment Sub-Panel (image_c62819.jpg)")]
    [SerializeField] private TextMeshProUGUI _equipWeightText;
    [SerializeField] private TextMeshProUGUI _attackPowerText;
    [SerializeField] private TextMeshProUGUI _guardAbsorptionText;
    [SerializeField] private TextMeshProUGUI _scalingGradeText;
    [SerializeField] private TextMeshProUGUI _requirementsText;
    [SerializeField] private TextMeshProUGUI _equipmentDescText;

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

        // 1. 소모품 계열 (image_945d19.jpg 구조)
        if (data is CountableItemData countableData)
        {
            if (_consumablePanel != null) _consumablePanel.SetActive(true);
            if (_equipmentPanel != null) _equipmentPanel.SetActive(false);

            int curAmount = item is CountableItem countable ? countable.CurrentAmount : 1;
            if (_possessionCountText != null) _possessionCountText.text = $"{curAmount} / {countableData.MaxStack}";
            if (_storageCountText != null) _storageCountText.text = $"- / {data.MaxStorageCount}";
            if (_effectSummaryText != null) _effectSummaryText.text = data.GetEffectSummaryText();
            if (_consumableDescText != null) _consumableDescText.text = data.Description;
        }
        // 2. 장비 계열 (image_c62819.jpg 구조)
        else if (data is EquipmentItemData equipData)
        {
            if (_consumablePanel != null) _consumablePanel.SetActive(false);
            if (_equipmentPanel != null) _equipmentPanel.SetActive(true);

            // [점검 완료] 미구현된 내구도(Durability) 텍스트를 제거하고 중량만 표기
            if (_equipWeightText != null)
            {
                _equipWeightText.text = $"중량 {data.Weight:F1}";
            }

            if (_attackPowerText != null)
            {
                if (equipData is WeaponItemData weapon)
                    _attackPowerText.text = $"물리: {weapon.BaseDamage:F0}\n강인도감쇄: {weapon.BasePoiseDamage:F0}\n치명: {weapon.Riposte_MV:F0}";
                else
                    _attackPowerText.text = "물리: -\n강인도감쇄: -\n치명: -";
            }

            if (_guardAbsorptionText != null)
            {
                if (equipData is ShieldItemData shield)
                    _guardAbsorptionText.text = $"물리컷: {shield.PhysicalAbsorption * 100f:F1}%\n버티기: {shield.Stability:F0}\n패링창: {shield.ParryWindowDuration:F2}s";
                else if (equipData is ArmorItemData armor)
                    _guardAbsorptionText.text = $"방어력: {armor.FlatDefense:F0}\n경감률: {armor.ArmorAbsorption * 100f:F1}%\n강인도: +{armor.PoiseBonus:F1}";
                else
                    _guardAbsorptionText.text = "물리컷: -\n버티기: -";
            }

            if (_scalingGradeText != null)
            {
                _scalingGradeText.text = $"근력: {GetGradeString(data.StrScalingGrade)}   기량: {GetGradeString(data.DexScalingGrade)}";
            }

            if (_requirementsText != null)
            {
                _requirementsText.text = $"근력: {equipData.RequiredStrength}   기량: {equipData.RequiredDexterity}";
            }

            if (_equipmentDescText != null) _equipmentDescText.text = data.Description;
        }
    }

    public void ClearDetails()
    {
        if (_itemIcon != null) _itemIcon.gameObject.SetActive(false);
        if (_itemNameText != null) _itemNameText.text = "아이템을 선택하십시오";
        if (_categoryText != null) _categoryText.text = string.Empty;

        if (_consumablePanel != null) _consumablePanel.SetActive(false);
        if (_equipmentPanel != null) _equipmentPanel.SetActive(false);
    }

    private string GetGradeString(StatScalingGrade grade) => grade == StatScalingGrade.None ? "-" : grade.ToString();
}