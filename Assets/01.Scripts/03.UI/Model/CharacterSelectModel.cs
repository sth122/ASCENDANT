public class CharacterSelectModel : IUIModel
{
    public const int MaxCharacterSlots = 3;

    public SaveData[] Slots { get; } = new SaveData[MaxCharacterSlots];
    public int CurrentSlotIndex { get; set; } = 0;
    public int InitialLevel { get; private set; } = 1;
    public int InitialVigor { get; private set; } = 10;
    public int InitialEndurance { get; private set; } = 10;
    public int InitialStrength { get; private set; } = 10;
    public int InitialDexterity { get; private set; } = 10;
    public float InitialMaxHp { get; private set; } = 670f;
    public float InitialMaxStamina { get; private set; } = 98f;
    public float InitialMaxEquipWeight { get; private set; } = 52f;

    public void RefreshSlots()
    {
        for (int i = 0; i < MaxCharacterSlots; i++)
        {
            Slots[i] = SaveManager.Instance != null ? SaveManager.Instance.PeekSlotData(i) : null;
        }
    }

    public void LoadInitialStatsPreview()
    {
        if (DataManager.Instance != null &&
            DataManager.Instance.TryGetSingle<PlayerStatusSO>(out PlayerStatusSO statusSO) &&
            statusSO.TryGetData(UnitId.Player, out PlayerInfo baseInfo))
        {
            InitialLevel = baseInfo.SoulLevel;
            InitialVigor = baseInfo.Vigor;
            InitialEndurance = baseInfo.Endurance;
            InitialStrength = baseInfo.Strength;
            InitialDexterity = baseInfo.Dexterity;
            InitialMaxHp = baseInfo.CalculateMaxHp();
            InitialMaxStamina = baseInfo.CalculateMaxStamina();
            InitialMaxEquipWeight = baseInfo.CalculateMaxEquipWeight();
        }
    }

    public bool HasCharacterInSlot(int index)
    {
        return index >= 0 && index < MaxCharacterSlots && Slots[index] != null && Slots[index].IsCharacterCreated;
    }

    public int FindFirstEmptySlot()
    {
        for (int i = 0; i < MaxCharacterSlots; i++)
        {
            if (!HasCharacterInSlot(i)) return i;
        }
        return -1;
    }

    public int GetCreatedCharacterCount()
    {
        int count = 0;
        for (int i = 0; i < MaxCharacterSlots; i++)
        {
            if (HasCharacterInSlot(i)) count++;
        }
        return count;
    }

    public void Reset()
    {
        CurrentSlotIndex = 0;
        for (int i = 0; i < MaxCharacterSlots; i++) Slots[i] = null;
    }
}