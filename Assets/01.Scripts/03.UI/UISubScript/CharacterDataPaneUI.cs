using TMPro;
using UnityEngine;

public class CharacterDataPaneUI : MonoBehaviour
{
    [Header("Level Info")]
    [SerializeField] private TextMeshProUGUI _soulLevelText;

    [Header("Core Attributes")]
    [SerializeField] private TextMeshProUGUI _vigorText;
    [SerializeField] private TextMeshProUGUI _enduranceText;
    [SerializeField] private TextMeshProUGUI _strengthText;
    [SerializeField] private TextMeshProUGUI _dexterityText;

    [Header("Resource Gauges")]
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private TextMeshProUGUI _staminaText;

    [Header("Combat & Defense")]
    [SerializeField] private TextMeshProUGUI _attackPowerText;
    [SerializeField] private TextMeshProUGUI _defenseText;
    [SerializeField] private TextMeshProUGUI _absorptionText;
    [SerializeField] private TextMeshProUGUI _poiseText;
    [SerializeField] private TextMeshProUGUI _equipWeightText;
    [SerializeField] private TextMeshProUGUI _weightRatioText;

    public void UpdateCharacterData(InventoryModel model)
    {
        if (model == null) return;

        if (_soulLevelText != null) _soulLevelText.text = $"레벨: {model.SoulLevel}";
        if (_hpText != null) _hpText.text = $"HP: {model.CurrentHp:F0} / {model.MaxHp:F0}";
        if (_staminaText != null) _staminaText.text = $"스태미너: {model.CurrentStamina:F0} / {model.MaxStamina:F0}";

        if (_vigorText != null) _vigorText.text = $"생명력: {model.Vigor}";
        if (_enduranceText != null) _enduranceText.text = $"지구력: {model.Endurance}";

        if (_strengthText != null)
            _strengthText.text = model.BonusStrength > 0 ? $"근력: {model.Strength} (+{model.BonusStrength})" : $"근력: {model.Strength}";
        if (_dexterityText != null)
            _dexterityText.text = model.BonusDexterity > 0 ? $"기량: {model.Dexterity} (+{model.BonusDexterity})" : $"기량: {model.Dexterity}";

        float ratio = model.MaxWeight > 0f ? (model.CurrentWeight / model.MaxWeight) * 100f : 0f;
        if (_attackPowerText != null) _attackPowerText.text = $"공격력: {model.CurrentAR:F0}";
        if (_defenseText != null) _defenseText.text = $"물리 방어력: {model.FlatDefense:F0}";
        if (_absorptionText != null) _absorptionText.text = $"경감률: {model.Absorption * 100f:F1}%";
        if (_poiseText != null) _poiseText.text = $"강인도: {model.CurrentPoise:F1}";
        if (_equipWeightText != null) _equipWeightText.text = $"장비 중량: {model.CurrentWeight:F1} / {model.MaxWeight:F1}";
        if (_weightRatioText != null) _weightRatioText.text = $"{ratio:F1}% ({model.CurrentRollTier})";
    }
}