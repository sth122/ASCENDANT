using TMPro;
using UnityEngine;

/// <summary>
/// 다크소울 3 인벤토리 우측에 위치하는 캐릭터 상태 데이터 UI 컴포넌트
/// </summary>
public class CharacterDataPaneUI : MonoBehaviour
{
    [Header("Level & Souls")]
    [SerializeField] private TextMeshProUGUI _soulLevelText;
    [SerializeField] private TextMeshProUGUI _currentSoulsText;

    [Header("Core Attributes")]
    [SerializeField] private TextMeshProUGUI _vigorText;
    [SerializeField] private TextMeshProUGUI _enduranceText;
    [SerializeField] private TextMeshProUGUI _strengthText;
    [SerializeField] private TextMeshProUGUI _dexterityText;

    [Header("Resource Gauges")]
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private TextMeshProUGUI _staminaText;

    [Header("Equip Load & Defense")]
    [SerializeField] private TextMeshProUGUI _equipWeightText;
    [SerializeField] private TextMeshProUGUI _weightRatioText;
    [SerializeField] private TextMeshProUGUI _poiseText;
    [SerializeField] private TextMeshProUGUI _attackPowerText;

    public void UpdateCharacterData(PlayerInfo info, float currentHp, float currentStamina, float currentPoise, float currentWeight, RollTier rollTier, float currentAR)
    {
        if (info == null) return;

        if (_soulLevelText != null) _soulLevelText.text = info.SoulLevel.ToString();
        if (_currentSoulsText != null) _currentSoulsText.text = info.CurrentSouls.ToString("N0");

        if (_vigorText != null) _vigorText.text = info.Vigor.ToString();
        if (_enduranceText != null) _enduranceText.text = info.Endurance.ToString();
        if (_strengthText != null) _strengthText.text = info.Strength.ToString();
        if (_dexterityText != null) _dexterityText.text = info.Dexterity.ToString();

        float maxHp = info.CalculateMaxHp();
        float maxStamina = info.CalculateMaxStamina();
        float maxWeight = info.CalculateMaxEquipWeight();
        float ratio = maxWeight > 0f ? (currentWeight / maxWeight) * 100f : 0f;

        if (_hpText != null) _hpText.text = $"{currentHp:F0} / {maxHp:F0}";
        if (_staminaText != null) _staminaText.text = $"{currentStamina:F0} / {maxStamina:F0}";
        if (_equipWeightText != null) _equipWeightText.text = $"{currentWeight:F1} / {maxWeight:F1}";
        if (_weightRatioText != null) _weightRatioText.text = $"{ratio:F1}% ({rollTier})";
        if (_poiseText != null) _poiseText.text = $"{currentPoise:F1}";
        if (_attackPowerText != null) _attackPowerText.text = $"{currentAR:F0}";
    }
}