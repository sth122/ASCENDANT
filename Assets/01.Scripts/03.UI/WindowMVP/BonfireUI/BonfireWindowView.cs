// BonfireWindowView.cs 전체 교체
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다크소울 3 스타일 화톳불 레벨업 전용 창 (InteractionPrompt에서 '레벨 업' 선택 시 오픈)
/// </summary>
public class BonfireWindowView : UIWindowView
{
    [Header("Left Column - Level & Souls")]
    [SerializeField] private TextMeshProUGUI _levelCurrentText;
    [SerializeField] private TextMeshProUGUI _levelPreviewText;
    [SerializeField] private TextMeshProUGUI _soulsCurrentText;
    [SerializeField] private TextMeshProUGUI _soulsRemainingText;
    [SerializeField] private TextMeshProUGUI _requiredSoulsText;

    [Header("Left Column - Core Attributes (Current -> Preview)")]
    [SerializeField] private TextMeshProUGUI _vigorCurrentText;
    [SerializeField] private TextMeshProUGUI _vigorPreviewText;
    [SerializeField] private Button _vigorMinusBtn;
    [SerializeField] private Button _vigorPlusBtn;

    [SerializeField] private TextMeshProUGUI _enduranceCurrentText;
    [SerializeField] private TextMeshProUGUI _endurancePreviewText;
    [SerializeField] private Button _enduranceMinusBtn;
    [SerializeField] private Button _endurancePlusBtn;

    [SerializeField] private TextMeshProUGUI _strengthCurrentText;
    [SerializeField] private TextMeshProUGUI _strengthPreviewText;
    [SerializeField] private Button _strengthMinusBtn;
    [SerializeField] private Button _strengthPlusBtn;

    [SerializeField] private TextMeshProUGUI _dexterityCurrentText;
    [SerializeField] private TextMeshProUGUI _dexterityPreviewText;
    [SerializeField] private Button _dexterityMinusBtn;
    [SerializeField] private Button _dexterityPlusBtn;

    [Header("Right Column - Derived Stats Preview (Current -> Preview)")]
    [SerializeField] private TextMeshProUGUI _hpCurrentText;
    [SerializeField] private TextMeshProUGUI _hpPreviewText;
    [SerializeField] private TextMeshProUGUI _staminaCurrentText;
    [SerializeField] private TextMeshProUGUI _staminaPreviewText;
    [SerializeField] private TextMeshProUGUI _equipWeightCurrentText;
    [SerializeField] private TextMeshProUGUI _equipWeightPreviewText;
    [SerializeField] private TextMeshProUGUI _attackPowerCurrentText;
    [SerializeField] private TextMeshProUGUI _attackPowerPreviewText;
    [SerializeField] private TextMeshProUGUI _attackSpeedCurrentText;
    [SerializeField] private TextMeshProUGUI _attackSpeedPreviewText;
    [SerializeField] private TextMeshProUGUI _defenseCurrentText;
    [SerializeField] private TextMeshProUGUI _defensePreviewText;

    [Header("Level Up Action Buttons")]
    [SerializeField] private Button _confirmLevelUpBtn;
    [SerializeField] private Button _closeLevelUpPanelBtn;

    private static readonly Color NormalTextColor = new Color(0.92f, 0.88f, 0.80f, 1f);
    private static readonly Color UpgradeBlueColor = new Color(0.35f, 0.68f, 1.0f, 1f);
    private static readonly Color SpendRedColor = new Color(0.92f, 0.35f, 0.28f, 1f);

    public Button VigorMinusBtn => _vigorMinusBtn;
    public Button VigorPlusBtn => _vigorPlusBtn;
    public Button EnduranceMinusBtn => _enduranceMinusBtn;
    public Button EndurancePlusBtn => _endurancePlusBtn;
    public Button StrengthMinusBtn => _strengthMinusBtn;
    public Button StrengthPlusBtn => _strengthPlusBtn;
    public Button DexterityMinusBtn => _dexterityMinusBtn;
    public Button DexterityPlusBtn => _dexterityPlusBtn;

    public Button ConfirmLevelUpBtn => _confirmLevelUpBtn;
    public Button CloseLevelUpPanelBtn => _closeLevelUpPanelBtn;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.Window;
        _pausePolicy = WindowPausePolicy.RealtimeCombat;
    }

    public void RefreshDisplay(BonfireModel m)
    {
        int totalCost = m.CalculateTotalSoulCost();
        int remainingSouls = m.BaseSouls - totalCost;
        bool hasPending = m.TotalPendingPoints > 0;

        SetStatPair(_levelCurrentText, _levelPreviewText, m.BaseSoulLevel, m.PreviewSoulLevel);

        if (_soulsCurrentText != null) _soulsCurrentText.text = m.BaseSouls.ToString("N0");
        if (_soulsRemainingText != null)
        {
            _soulsRemainingText.text = remainingSouls.ToString("N0");
            _soulsRemainingText.color = hasPending ? SpendRedColor : NormalTextColor;
        }
        if (_requiredSoulsText != null)
        {
            _requiredSoulsText.text = hasPending ? totalCost.ToString("N0") : m.GetNextSinglePointCost().ToString("N0");
        }

        SetStatPair(_vigorCurrentText, _vigorPreviewText, m.BaseVigor, m.PreviewVigor);
        SetStatPair(_enduranceCurrentText, _endurancePreviewText, m.BaseEndurance, m.PreviewEndurance);
        SetStatPair(_strengthCurrentText, _strengthPreviewText, m.BaseStrength, m.PreviewStrength);
        SetStatPair(_dexterityCurrentText, _dexterityPreviewText, m.BaseDexterity, m.PreviewDexterity);

        SetFloatStatPair(_hpCurrentText, _hpPreviewText, m.CurrentMaxHp, m.PreviewMaxHp, "F0");
        SetFloatStatPair(_staminaCurrentText, _staminaPreviewText, m.CurrentMaxStamina, m.PreviewMaxStamina, "F0");
        SetFloatStatPair(_equipWeightCurrentText, _equipWeightPreviewText, m.CurrentMaxEquipWeight, m.PreviewMaxEquipWeight, "F1");
        SetFloatStatPair(_attackPowerCurrentText, _attackPowerPreviewText, m.CurrentAR, m.PreviewAR, "F0");
        SetFloatStatPair(_attackSpeedCurrentText, _attackSpeedPreviewText, m.CurrentAttackSpeedPercent, m.PreviewAttackSpeedPercent, "F0", "%");
        SetFloatStatPair(_defenseCurrentText, _defensePreviewText, m.CurrentFlatDefense, m.PreviewFlatDefense, "F0");

        bool canAddMore = m.CanAffordNextPoint();
        if (_vigorPlusBtn != null) _vigorPlusBtn.interactable = canAddMore;
        if (_endurancePlusBtn != null) _endurancePlusBtn.interactable = canAddMore;
        if (_strengthPlusBtn != null) _strengthPlusBtn.interactable = canAddMore;
        if (_dexterityPlusBtn != null) _dexterityPlusBtn.interactable = canAddMore;

        if (_vigorMinusBtn != null) _vigorMinusBtn.interactable = m.PendingVigor > 0;
        if (_enduranceMinusBtn != null) _enduranceMinusBtn.interactable = m.PendingEndurance > 0;
        if (_strengthMinusBtn != null) _strengthMinusBtn.interactable = m.PendingStrength > 0;
        if (_dexterityMinusBtn != null) _dexterityMinusBtn.interactable = m.PendingDexterity > 0;

        if (_confirmLevelUpBtn != null) _confirmLevelUpBtn.interactable = hasPending;
    }

    private void SetStatPair(TextMeshProUGUI curTxt, TextMeshProUGUI prevTxt, int curVal, int nextVal)
    {
        if (curTxt != null) curTxt.text = curVal.ToString();
        if (prevTxt != null)
        {
            prevTxt.text = nextVal.ToString();
            prevTxt.color = nextVal > curVal ? UpgradeBlueColor : NormalTextColor;
        }
    }

    private void SetFloatStatPair(TextMeshProUGUI curTxt, TextMeshProUGUI prevTxt, float curVal, float nextVal, string format, string suffix = "")
    {
        if (curTxt != null) curTxt.text = curVal.ToString(format) + suffix;
        if (prevTxt != null)
        {
            prevTxt.text = nextVal.ToString(format) + suffix;
            prevTxt.color = (nextVal - curVal) > 0.05f ? UpgradeBlueColor : NormalTextColor;
        }
    }
}