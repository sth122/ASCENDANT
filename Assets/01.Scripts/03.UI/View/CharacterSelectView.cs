using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택(window_character_selection), 캐릭터 생성(window_character_creation),
/// 캐릭터 삭제 팝업(popup_delete_character) 3개 패널을 통합 제어하는 뷰 (TMP)
/// </summary>
public class CharacterSelectView : UIWindowView
{
    [Header("3 Sub-Window Roots")]
    [SerializeField] private GameObject _selectionWindowRoot;
    [SerializeField] private GameObject _creationWindowRoot;
    [SerializeField] private GameObject _deletePopupRoot;

    [Header("1. Selection Window - Character Previews")]
    [SerializeField] private Image _centerCharacterImage;
    [SerializeField] private Image _leftCharacterImage;
    [SerializeField] private Image _rightCharacterImage;
    [SerializeField] private Sprite[] _slotCharacterSprites = new Sprite[3];
    [SerializeField] private Sprite _emptySlotSilhouetteSprite;

    [Header("1. Selection Window - Controls & Info")]
    [SerializeField] private Button _prevSlotButton;
    [SerializeField] private Button _nextSlotButton;
    [SerializeField] private TextMeshProUGUI _slotIndexText;
    [SerializeField] private TextMeshProUGUI _characterNameText;
    [SerializeField] private TextMeshProUGUI _characterClassText;
    [SerializeField] private TextMeshProUGUI _characterLevelText;
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _openCreateButton;
    [SerializeField] private Button _openDeleteButton;
    [SerializeField] private Button _closeMenuButton;

    [Header("2. Creation Window - Name, Initial Stats & Buttons")]
    [SerializeField] private TMP_InputField _nameInputField;
    [SerializeField] private Image _creationPreviewImage;
    [SerializeField] private TextMeshProUGUI _initialStatsText;
    [SerializeField] private Button _confirmCreateButton;
    [SerializeField] private Button _creationBackButton;

    [Header("3. Delete Confirmation Popup")]
    [SerializeField] private TextMeshProUGUI _deleteTitleText;
    [SerializeField] private TextMeshProUGUI _deleteMessageText;
    [SerializeField] private Button _deleteYesButton;
    [SerializeField] private Button _deleteNoButton;

    public Button PrevSlotButton => _prevSlotButton;
    public Button NextSlotButton => _nextSlotButton;
    public Button PlayButton => _playButton;
    public Button OpenCreateButton => _openCreateButton;
    public Button OpenDeleteButton => _openDeleteButton;
    public Button CloseMenuButton => _closeMenuButton;

    public TMP_InputField NameInputField => _nameInputField;
    public Button ConfirmCreateButton => _confirmCreateButton;
    public Button CreationBackButton => _creationBackButton;

    public Button DeleteYesButton => _deleteYesButton;
    public Button DeleteNoButton => _deleteNoButton;

    public override void InitializeView()
    {
        base.InitializeView();
        Layer = UILayer.Window;
        ShowSelectionWindow();
    }

    public override async Cysharp.Threading.Tasks.UniTask ShowAsync(float duration = 0.1f, System.Threading.CancellationToken ct = default)
    {
        EventBusSystem.Publish(new TitleIntroVisibilityEvent(false));
        await base.ShowAsync(duration, ct);
    }

    public override async Cysharp.Threading.Tasks.UniTask CloseWindowAsync(float duration = 0.15f)
    {
        await base.CloseWindowAsync(duration);
        EventBusSystem.Publish(new TitleIntroVisibilityEvent(true));
    }

    public void ShowSelectionWindow()
    {
        if (_selectionWindowRoot != null) _selectionWindowRoot.SetActive(true);
        if (_creationWindowRoot != null) _creationWindowRoot.SetActive(false);
        if (_deletePopupRoot != null) _deletePopupRoot.SetActive(false);
    }

    public void ShowCreationWindow(int targetSlotIndex, CharacterSelectModel model)
    {
        if (_selectionWindowRoot != null) _selectionWindowRoot.SetActive(false);
        if (_creationWindowRoot != null) _creationWindowRoot.SetActive(true);
        if (_deletePopupRoot != null) _deletePopupRoot.SetActive(false);

        if (_nameInputField != null)
        {
            _nameInputField.text = $"잿빛 기사 {targetSlotIndex + 1}";
        }

        if (_creationPreviewImage != null && _slotCharacterSprites != null && _slotCharacterSprites.Length > 0)
        {
            int idx = Mathf.Clamp(targetSlotIndex, 0, _slotCharacterSprites.Length - 1);
            _creationPreviewImage.sprite = _slotCharacterSprites[idx];
        }

        if (_initialStatsText != null && model != null)
        {
            _initialStatsText.text =
                $"[ 초기 능력치 - 슬롯 {targetSlotIndex + 1} ]\n\n" +
                $"레벨 (Level) : {model.InitialLevel}\n" +
                $"생명력 (Vigor) : {model.InitialVigor}\n" +
                $"지구력 (Endurance) : {model.InitialEndurance}\n" +
                $"근력 (Strength) : {model.InitialStrength}\n" +
                $"기량 (Dexterity) : {model.InitialDexterity}\n\n" +
                $"최대 HP : {model.InitialMaxHp:F0}\n" +
                $"최대 스태미나 : {model.InitialMaxStamina:F0}\n" +
                $"장비 한계 중량 : {model.InitialMaxEquipWeight:F1}";
        }
    }

    public void ShowDeletePopup(SaveData targetSave)
    {
        if (_deletePopupRoot != null) _deletePopupRoot.SetActive(true);

        if (_deleteTitleText != null) _deleteTitleText.text = "캐릭터 삭제";

        if (_deleteMessageText != null && targetSave != null)
        {
            int lv = targetSave.PlayerData != null ? targetSave.PlayerData.SoulLevel : 1;
            _deleteMessageText.text = $"'{targetSave.CharacterName}' (Lv.{lv}) 캐릭터를 삭제하시겠습니까?\n삭제된 세이브 데이터는 복구할 수 없습니다.";
        }
    }

    public void HideDeletePopup()
    {
        if (_deletePopupRoot != null) _deletePopupRoot.SetActive(false);
    }

    public void RenderSelectionSlots(CharacterSelectModel model)
    {
        int centerIdx = model.CurrentSlotIndex;
        int leftIdx = (centerIdx + CharacterSelectModel.MaxCharacterSlots - 1) % CharacterSelectModel.MaxCharacterSlots;
        int rightIdx = (centerIdx + 1) % CharacterSelectModel.MaxCharacterSlots;

        ApplySlotPortrait(_centerCharacterImage, centerIdx, model.HasCharacterInSlot(centerIdx), isCenter: true);
        ApplySlotPortrait(_leftCharacterImage, leftIdx, model.HasCharacterInSlot(leftIdx), isCenter: false);
        ApplySlotPortrait(_rightCharacterImage, rightIdx, model.HasCharacterInSlot(rightIdx), isCenter: false);

        if (_slotIndexText != null)
        {
            _slotIndexText.text = $"SLOT {centerIdx + 1} / {CharacterSelectModel.MaxCharacterSlots}";
        }

        bool hasChar = model.HasCharacterInSlot(centerIdx);
        SaveData currentSave = model.Slots[centerIdx];

        if (hasChar && currentSave != null)
        {
            int lv = currentSave.PlayerData != null ? currentSave.PlayerData.SoulLevel : 1;
            if (_characterNameText != null) _characterNameText.text = currentSave.CharacterName;
            if (_characterClassText != null) _characterClassText.text = currentSave.LastSavedScene.ToString();
            if (_characterLevelText != null) _characterLevelText.text = $"Lv.{lv}";
        }
        else
        {
            if (_characterNameText != null) _characterNameText.text = "비어 있는 슬롯";
            if (_characterClassText != null) _characterClassText.text = "-";
            if (_characterLevelText != null) _characterLevelText.text = string.Empty;
        }

        if (_playButton != null) _playButton.interactable = hasChar;
        if (_openDeleteButton != null) _openDeleteButton.interactable = hasChar;

        bool canCreateMore = model.GetCreatedCharacterCount() < CharacterSelectModel.MaxCharacterSlots;
        if (_openCreateButton != null) _openCreateButton.interactable = canCreateMore;
    }

    private void ApplySlotPortrait(Image targetImg, int slotIdx, bool hasChar, bool isCenter)
    {
        if (targetImg == null) return;

        if (_slotCharacterSprites != null && slotIdx >= 0 && slotIdx < _slotCharacterSprites.Length && _slotCharacterSprites[slotIdx] != null)
        {
            targetImg.sprite = _slotCharacterSprites[slotIdx];
        }
        else if (_emptySlotSilhouetteSprite != null)
        {
            targetImg.sprite = _emptySlotSilhouetteSprite;
        }

        if (hasChar)
        {
            targetImg.color = isCenter ? Color.white : new Color(0.55f, 0.50f, 0.45f, 0.85f);
        }
        else
        {
            targetImg.color = isCenter ? new Color(0.25f, 0.23f, 0.20f, 0.6f) : new Color(0.15f, 0.14f, 0.12f, 0.4f);
        }
    }
}