using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BonfireWindowView : UIWindowView
{
    [Header("Menu Buttons")]
    [SerializeField] private Button _restButton;
    [SerializeField] private Button _levelUpMenuButton;
    [SerializeField] private Button _leaveButton;

    [Header("Level Up Panel")]
    [SerializeField] private GameObject _levelUpPanel;
    [SerializeField] private TextMeshProUGUI _currentSoulText;
    [SerializeField] private TextMeshProUGUI _requiredSoulText;
    [SerializeField] private TextMeshProUGUI _targetLevelText;

    [Header("Attribute Up Buttons")]
    [SerializeField] private Button _vigPlusBtn;
    [SerializeField] private Button _endPlusBtn;
    [SerializeField] private Button _strPlusBtn;
    [SerializeField] private Button _dexPlusBtn;
    [SerializeField] private Button _confirmLevelUpBtn;

    [Header("Attribute Preview Texts")]
    [SerializeField] private TextMeshProUGUI _vigValText;
    [SerializeField] private TextMeshProUGUI _endValText;
    [SerializeField] private TextMeshProUGUI _strValText;
    [SerializeField] private TextMeshProUGUI _dexValText;

    public Button RestButton => _restButton;
    public Button LevelUpMenuButton => _levelUpMenuButton;
    public Button LeaveButton => _leaveButton;

    public GameObject LevelUpPanel => _levelUpPanel;
    public TextMeshProUGUI CurrentSoulText => _currentSoulText;
    public TextMeshProUGUI RequiredSoulText => _requiredSoulText;
    public TextMeshProUGUI TargetLevelText => _targetLevelText;

    public Button VigPlusBtn => _vigPlusBtn;
    public Button EndPlusBtn => _endPlusBtn;
    public Button StrPlusBtn => _strPlusBtn;
    public Button DexPlusBtn => _dexPlusBtn;
    public Button ConfirmLevelUpBtn => _confirmLevelUpBtn;

    public TextMeshProUGUI VigValText => _vigValText;
    public TextMeshProUGUI EndValText => _endValText;
    public TextMeshProUGUI StrValText => _strValText;
    public TextMeshProUGUI DexValText => _dexValText;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.Window;
        _pausePolicy = WindowPausePolicy.RealtimeCombat; // 실시간 소울라이크 창 유지
    }
}