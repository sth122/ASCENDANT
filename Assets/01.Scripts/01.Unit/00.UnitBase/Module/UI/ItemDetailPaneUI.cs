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

    [Header("Consumable Sub-Panel")]
    [SerializeField] private TextMeshProUGUI _possessionCountText;
    [SerializeField] private TextMeshProUGUI _storageCountText;
    [SerializeField] private TextMeshProUGUI _effectSummaryText;
    [SerializeField] private TextMeshProUGUI _consumableDescText;

    [Header("Equipment Sub-Panel")]
    [SerializeField] private TextMeshProUGUI _equipWeightText;
    [SerializeField] private TextMeshProUGUI _attackPowerText;
    [SerializeField] private TextMeshProUGUI _guardAbsorptionText;
    [SerializeField] private TextMeshProUGUI _scalingGradeText;
    [SerializeField] private TextMeshProUGUI _requirementsText;
    [SerializeField] private TextMeshProUGUI _equipmentDescText;

    private const string BetterHex = "#58B4FF"; // 상승(파란색)
    private const string WorseHex = "#FF5A4E";  // 하락(붉은색)

    public void ShowDetails(Item item, Inventory inventory = null, PlayerController player = null)
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

        // 1. 소모품 계열
        if (data is CountableItemData countableData)
        {
            if (_consumablePanel != null) _consumablePanel.SetActive(true);
            if (_equipmentPanel != null) _equipmentPanel.SetActive(false);

            int curAmount = item is CountableItem countable ? countable.CurrentAmount : 1;
            if (_possessionCountText != null) _possessionCountText.text = $"{curAmount} / {countableData.MaxStack}";
            if (_storageCountText != null) _storageCountText.text = $"- / {data.MaxStorageCount}";
            if (_effectSummaryText != null) _effectSummaryText.text = data.GetEffectSummaryText();
            if (_consumableDescText != null) _consumableDescText.text = data.Description;
            return;
        }

        // 2. 장비 계열 (현재 장착 중인 동일 부위 장비와 수치 비교)
        if (data is EquipmentItemData equipData)
        {
            if (_consumablePanel != null) _consumablePanel.SetActive(false);
            if (_equipmentPanel != null) _equipmentPanel.SetActive(true);

            EquipmentItem equipped = inventory != null ? inventory.GetEquippedItem(equipData.EquipSlot) : null;
            // 동일한 인스턴스(이미 장착된 아이템)라면 비교 생략
            if (equipped != null && equipped.InstanceGuid == item.InstanceGuid)
            {
                equipped = null;
            }

            // 중량 비교 (가벼워지면 파랑, 무거워지면 빨강)
            if (_equipWeightText != null)
            {
                if (equipped != null && equipped.Data != null)
                {
                    float diff = data.Weight - equipped.Data.Weight;
                    _equipWeightText.text = $"중량 {data.Weight:F1} {FormatDiff(diff, "F1", lowerIsBetter: true)}";
                }
                else
                {
                    _equipWeightText.text = $"중량 {data.Weight:F1}";
                }
            }

            // 공격력 / 보너스 스탯 비교
            if (_attackPowerText != null)
            {
                if (equipData is WeaponItemData weapon)
                {
                    WeaponItemData curWpn = equipped?.Data as WeaponItemData;
                    string dmgDiff = curWpn != null ? FormatDiff(weapon.BaseDamage - curWpn.BaseDamage, "F0") : string.Empty;
                    string poiseDiff = curWpn != null ? FormatDiff(weapon.BasePoiseDamage - curWpn.BasePoiseDamage, "F0") : string.Empty;
                    string critDiff = curWpn != null ? FormatDiff(weapon.Riposte_MV - curWpn.Riposte_MV, "F0") : string.Empty;

                    _attackPowerText.text =
                        $"물리: {weapon.BaseDamage:F0} {dmgDiff}\n" +
                        $"강인도감쇄: {weapon.BasePoiseDamage:F0} {poiseDiff}\n" +
                        $"치명: {weapon.Riposte_MV:F0} {critDiff}";
                }
                else if (equipData is AccessoryItemData acc)
                {
                    AccessoryItemData curAcc = equipped?.Data as AccessoryItemData;
                    float atkDiffVal = acc.BonusAttackPower - (curAcc != null ? curAcc.BonusAttackPower : 0f);
                    float hpDiffVal = acc.BonusMaxHp - (curAcc != null ? curAcc.BonusMaxHp : 0f);
                    float stmDiffVal = acc.BonusMaxStamina - (curAcc != null ? curAcc.BonusMaxStamina : 0f);

                    _attackPowerText.text =
                        $"추가 공격력: +{acc.BonusAttackPower:F0} {(curAcc != null ? FormatDiff(atkDiffVal, "F0") : "")}\n" +
                        $"최대 HP: +{acc.BonusMaxHp:F0} {(curAcc != null ? FormatDiff(hpDiffVal, "F0") : "")}\n" +
                        $"최대 스태미나: +{acc.BonusMaxStamina:F0} {(curAcc != null ? FormatDiff(stmDiffVal, "F0") : "")}";
                }
                else
                {
                    _attackPowerText.text = "물리: -\n강인도감쇄: -\n치명: -";
                }
            }

            // 방어 / 가드 성능 비교
            if (_guardAbsorptionText != null)
            {
                if (equipData is ShieldItemData shield)
                {
                    ShieldItemData curShd = equipped?.Data as ShieldItemData;
                    string cutDiff = curShd != null ? FormatDiff((shield.PhysicalAbsorption - curShd.PhysicalAbsorption) * 100f, "F1") : string.Empty;
                    string stbDiff = curShd != null ? FormatDiff(shield.Stability - curShd.Stability, "F0") : string.Empty;

                    _guardAbsorptionText.text =
                        $"물리컷: {shield.PhysicalAbsorption * 100f:F1}% {cutDiff}\n" +
                        $"버티기: {shield.Stability:F0} {stbDiff}\n" +
                        $"패링창: {shield.ParryWindowDuration:F2}s";
                }
                else if (equipData is ArmorItemData armor)
                {
                    ArmorItemData curArm = equipped?.Data as ArmorItemData;
                    string defDiff = curArm != null ? FormatDiff(armor.FlatDefense - curArm.FlatDefense, "F0") : string.Empty;
                    string absDiff = curArm != null ? FormatDiff((armor.ArmorAbsorption - curArm.ArmorAbsorption) * 100f, "F1") : string.Empty;
                    string psDiff = curArm != null ? FormatDiff(armor.PoiseBonus - curArm.PoiseBonus, "F1") : string.Empty;

                    _guardAbsorptionText.text =
                        $"방어력: {armor.FlatDefense:F0} {defDiff}\n" +
                        $"경감률: {armor.ArmorAbsorption * 100f:F1}% {absDiff}\n" +
                        $"강인도: +{armor.PoiseBonus:F1} {psDiff}";
                }
                else if (equipData is AccessoryItemData acc)
                {
                    AccessoryItemData curAcc = equipped?.Data as AccessoryItemData;
                    float defDiffVal = acc.BonusFlatDefense - (curAcc != null ? curAcc.BonusFlatDefense : 0f);
                    int strDiffVal = acc.BonusStrength - (curAcc != null ? curAcc.BonusStrength : 0);
                    int dexDiffVal = acc.BonusDexterity - (curAcc != null ? curAcc.BonusDexterity : 0);

                    _guardAbsorptionText.text =
                        $"추가 방어력: +{acc.BonusFlatDefense:F0} {(curAcc != null ? FormatDiff(defDiffVal, "F0") : "")}\n" +
                        $"근력 보너스: +{acc.BonusStrength} {(curAcc != null ? FormatDiff(strDiffVal, "F0") : "")}\n" +
                        $"기량 보너스: +{acc.BonusDexterity} {(curAcc != null ? FormatDiff(dexDiffVal, "F0") : "")}";
                }
                else
                {
                    _guardAbsorptionText.text = "물리컷: -\n버티기: -";
                }
            }

            if (_scalingGradeText != null)
            {
                _scalingGradeText.text = $"근력: {GetGradeString(data.StrScalingGrade)}   기량: {GetGradeString(data.DexScalingGrade)}";
            }

            if (_requirementsText != null)
            {
                int curStr = player != null && player.StatusController != null ? player.StatusController.PlayerInfoSource.Strength : 99;
                int curDex = player != null && player.StatusController != null ? player.StatusController.PlayerInfoSource.Dexterity : 99;

                string strText = curStr < equipData.RequiredStrength
                    ? $"<color={WorseHex}>근력: {equipData.RequiredStrength}</color>"
                    : $"근력: {equipData.RequiredStrength}";
                string dexText = curDex < equipData.RequiredDexterity
                    ? $"<color={WorseHex}>기량: {equipData.RequiredDexterity}</color>"
                    : $"기량: {equipData.RequiredDexterity}";

                _requirementsText.text = $"{strText}   {dexText}";
            }

            if (_equipmentDescText != null) _equipmentDescText.text = data.Description;
        }
    }

    private string FormatDiff(float diff, string format, bool lowerIsBetter = false)
    {
        if (Mathf.Abs(diff) < 0.05f) return "(0)";

        bool isPositiveGain = lowerIsBetter ? (diff < 0f) : (diff > 0f);
        string color = isPositiveGain ? BetterHex : WorseHex;
        string sign = diff > 0f ? "+" : "";
        return $"<color={color}>({sign}{diff.ToString(format)})</color>";
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