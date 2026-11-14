using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public readonly struct InteractionChoice
{
    public readonly string Label;
    public readonly Action OnSelected;
    public readonly bool IsInteractable;

    public InteractionChoice(string label, Action onSelected, bool isInteractable = true)
    {
        Label = label;
        OnSelected = onSelected;
        IsInteractable = isInteractable;
    }
}

/// <summary>
/// PlayerHUD 하단 중앙에 배치되는 다크소울 3 스타일 상호작용 프롬프트
/// - 근접 시: 단일 안내 문구 표시 (예: "[E] 화톳불에 쉰다")
/// - 상호작용 시: 다중 선택지(휴식한다 / 레벨 업 / 떠난다 등) 표시 및 방향키/휠/마우스 클릭 선택
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    [Header("Containers")]
    [SerializeField] private GameObject _singleHintRoot;
    [SerializeField] private TextMeshProUGUI _singleHintText;

    [SerializeField] private GameObject _choiceListRoot;
    [SerializeField] private TextMeshProUGUI _legendText;
    [SerializeField] private OptionRow[] _optionRows = new OptionRow[4];

    [Serializable]
    public struct OptionRow
    {
        public GameObject Root;
        public TextMeshProUGUI LabelText;
        public GameObject SelectedUnderline;
        public Button ClickButton;
    }

    private readonly List<InteractionChoice> _currentChoices = new List<InteractionChoice>(4);
    private int _selectedIndex = 0;
    private Action _onClosedCallback;

    public bool IsChoiceMenuOpen => _choiceListRoot != null && _choiceListRoot.activeSelf;

    private void Awake()
    {
        for (int i = 0; i < _optionRows.Length; i++)
        {
            int idx = i;
            if (_optionRows[i].ClickButton != null)
            {
                _optionRows[i].ClickButton.onClick.AddListener(() => ConfirmChoiceAt(idx));
            }
        }
        HideAll();
    }

    public void ShowSingleHint(string promptText)
    {
        if (IsChoiceMenuOpen) return;

        gameObject.SetActive(true);
        if (_choiceListRoot != null) _choiceListRoot.SetActive(false);
        if (_singleHintRoot != null) _singleHintRoot.SetActive(true);
        if (_singleHintText != null) _singleHintText.text = promptText;
    }

    public void OpenChoiceMenu(IReadOnlyList<InteractionChoice> choices, Action onClosed = null)
    {
        if (choices == null || choices.Count == 0) return;

        _currentChoices.Clear();
        for (int i = 0; i < choices.Count && i < _optionRows.Length; i++)
        {
            _currentChoices.Add(choices[i]);
        }

        _selectedIndex = 0;
        _onClosedCallback = onClosed;

        gameObject.SetActive(true);
        if (_singleHintRoot != null) _singleHintRoot.SetActive(false);
        if (_choiceListRoot != null) _choiceListRoot.SetActive(true);

        if (_legendText != null)
        {
            _legendText.text = "[E / Enter] : 결정    [ESC / Q] : 닫기";
        }

        InputSystemHandler.SetMode(InputMode.RealtimeUI);
        RefreshChoiceRows();
    }

    public void CloseChoiceMenu(bool restoreGameplayInput = true)
    {
        if (_choiceListRoot != null) _choiceListRoot.SetActive(false);
        _currentChoices.Clear();

        if (restoreGameplayInput && (UIManager.Instance == null || !UIManager.Instance.HasActiveModalOrWindow()))
        {
            InputSystemHandler.SetMode(InputMode.Gameplay);
        }

        Action cb = _onClosedCallback;
        _onClosedCallback = null;
        cb?.Invoke();
    }

    public void HideAll()
    {
        if (_singleHintRoot != null) _singleHintRoot.SetActive(false);
        if (_choiceListRoot != null) _choiceListRoot.SetActive(false);
        _currentChoices.Clear();
    }

    private void Update()
    {
        if (!IsChoiceMenuOpen || _currentChoices.Count == 0) return;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        // 1. 위/아래 선택 이동 (W/S, 방향키, 마우스 휠)
        int moveDelta = 0;
        if (kb != null)
        {
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) moveDelta = -1;
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) moveDelta = 1;
        }
        if (mouse != null)
        {
            float scrollY = mouse.scroll.ReadValue().y;
            if (scrollY > 0.1f) moveDelta = -1;
            else if (scrollY < -0.1f) moveDelta = 1;
        }

        if (moveDelta != 0)
        {
            _selectedIndex = (_selectedIndex + moveDelta + _currentChoices.Count) % _currentChoices.Count;
            SoundManager.Instance?.PlaySFX(SoundId.UI_Hover);
            RefreshChoiceRows();
        }

        // 2. 결정 (E / Enter)
        if (kb != null && (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
        {
            ConfirmChoiceAt(_selectedIndex);
            return;
        }

        // 3. 닫기 (Q / ESC)
        if (kb != null && (kb.escapeKey.wasPressedThisFrame))
        {
            SoundManager.Instance?.PlaySFX(SoundId.UI_Cancel);
            CloseChoiceMenu(restoreGameplayInput: true);
        }
    }

    public void SetHoveredIndex(int index)
    {
        if (index >= 0 && index < _currentChoices.Count && _selectedIndex != index)
        {
            _selectedIndex = index;
            RefreshChoiceRows();
        }
    }

    private void ConfirmChoiceAt(int index)
    {
        if (index < 0 || index >= _currentChoices.Count) return;

        InteractionChoice choice = _currentChoices[index];
        if (!choice.IsInteractable)
        {
            SoundManager.Instance?.PlaySFX(SoundId.UI_Cancel);
            return;
        }

        SoundManager.Instance?.PlaySFX(SoundId.UI_Confirm);
        Action action = choice.OnSelected;

        // 새 창(예: BonfireUI)을 여는 선택지일 수 있으므로 일단 프롬프트만 닫고 콜백 실행
        CloseChoiceMenu(restoreGameplayInput: true);
        action?.Invoke();
    }

    private void RefreshChoiceRows()
    {
        Color selectedColor = new Color(0.98f, 0.86f, 0.58f, 1f);
        Color normalColor = new Color(0.84f, 0.80f, 0.74f, 0.92f);
        Color disabledColor = new Color(0.45f, 0.43f, 0.40f, 0.6f);

        for (int i = 0; i < _optionRows.Length; i++)
        {
            OptionRow row = _optionRows[i];
            if (row.Root == null) continue;

            if (i < _currentChoices.Count)
            {
                row.Root.SetActive(true);
                InteractionChoice choice = _currentChoices[i];
                bool isSelected = (i == _selectedIndex);

                if (row.LabelText != null)
                {
                    row.LabelText.text = choice.Label;
                    row.LabelText.color = !choice.IsInteractable ? disabledColor : (isSelected ? selectedColor : normalColor);
                }

                if (row.SelectedUnderline != null)
                {
                    row.SelectedUnderline.SetActive(isSelected && choice.IsInteractable);
                }
            }
            else
            {
                row.Root.SetActive(false);
            }
        }
    }
}