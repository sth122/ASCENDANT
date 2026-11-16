public class BonfireModel : IUIModel
{
    public int BaseSoulLevel { get; private set; }
    public int BaseSouls { get; private set; }
    public int BaseVigor { get; private set; }
    public int BaseEndurance { get; private set; }
    public int BaseStrength { get; private set; }
    public int BaseDexterity { get; private set; }

    public int PendingVigor { get; set; }
    public int PendingEndurance { get; set; }
    public int PendingStrength { get; set; }
    public int PendingDexterity { get; set; }

    public float CurrentMaxHp { get; private set; }
    public float CurrentMaxStamina { get; private set; }
    public float CurrentMaxEquipWeight { get; private set; }
    public float CurrentAR { get; private set; }
    public float CurrentAttackSpeedPercent { get; private set; }
    public float CurrentFlatDefense { get; private set; }

    public float PreviewMaxHp { get; private set; }
    public float PreviewMaxStamina { get; private set; }
    public float PreviewMaxEquipWeight { get; private set; }
    public float PreviewAR { get; private set; }
    public float PreviewAttackSpeedPercent { get; private set; }
    public float PreviewFlatDefense { get; private set; }

    public int TotalPendingPoints => PendingVigor + PendingEndurance + PendingStrength + PendingDexterity;
    public int PreviewSoulLevel => BaseSoulLevel + TotalPendingPoints;
    public int PreviewVigor => BaseVigor + PendingVigor;
    public int PreviewEndurance => BaseEndurance + PendingEndurance;
    public int PreviewStrength => BaseStrength + PendingStrength;
    public int PreviewDexterity => BaseDexterity + PendingDexterity;

    public void CaptureFromPlayer(PlayerController player)
    {
        if (player == null || player.StatusController == null) return;

        PlayerStatusController status = player.StatusController;
        PlayerInfo info = status.PlayerInfoSource;

        BaseSoulLevel = info.SoulLevel;
        BaseSouls = info.CurrentSouls;
        BaseVigor = info.Vigor;
        BaseEndurance = info.Endurance;
        BaseStrength = info.Strength;
        BaseDexterity = info.Dexterity;

        ResetPending();
        RecalculatePreviewStats(player);
    }

    public void RecalculatePreviewStats(PlayerController player)
    {
        if (player == null || player.StatusController == null || player.StatusController.PlayerInfoSource == null) return;

        PlayerStatusController status = player.StatusController;
        PlayerInfo info = status.PlayerInfoSource;

        PlayerInfo baseSnapshot = (PlayerInfo)info.Clone();
        baseSnapshot.ApplySaveData(new PlayerSaveData(baseSnapshot, status.CurrentHp)
        {
            SoulLevel = BaseSoulLevel,
            CurrentSouls = BaseSouls,
            Vigor = BaseVigor,
            Endurance = BaseEndurance,
            Strength = BaseStrength,
            Dexterity = BaseDexterity
        });

        PlayerInfo previewSnapshot = (PlayerInfo)baseSnapshot.Clone();
        previewSnapshot.ApplyAttributeUpgrades(PendingVigor, PendingEndurance, PendingStrength, PendingDexterity);

        float equipHpBonus = status.MaxHp - baseSnapshot.CalculateMaxHp();
        CurrentMaxHp = status.MaxHp;
        PreviewMaxHp = previewSnapshot.CalculateMaxHp() + equipHpBonus;

        float equipStaminaBonus = status.MaxStamina - baseSnapshot.CalculateMaxStamina();
        CurrentMaxStamina = status.MaxStamina;
        PreviewMaxStamina = previewSnapshot.CalculateMaxStamina() + equipStaminaBonus;

        CurrentMaxEquipWeight = baseSnapshot.CalculateMaxEquipWeight();
        PreviewMaxEquipWeight = previewSnapshot.CalculateMaxEquipWeight();

        CurrentAttackSpeedPercent = baseSnapshot.CalculateAttackSpeedMultiplier(status.EquippedBonusDexterity) * 100f;
        PreviewAttackSpeedPercent = previewSnapshot.CalculateAttackSpeedMultiplier(status.EquippedBonusDexterity) * 100f;

        CurrentFlatDefense = baseSnapshot.FlatDefense + status.EquippedArmorFlatDefense;
        PreviewFlatDefense = previewSnapshot.FlatDefense + status.EquippedArmorFlatDefense;

        if (player.CombatModule != null)
        {
            CurrentAR = player.CombatModule.CalculatePreviewAR(BaseStrength, BaseDexterity);
            PreviewAR = player.CombatModule.CalculatePreviewAR(PreviewStrength, PreviewDexterity);
        }
    }

    public int CalculateTotalSoulCost()
    {
        int totalCost = 0;
        for (int i = 0; i < TotalPendingPoints; i++)
        {
            totalCost += PlayerInfo.CalculateRequiredSouls(BaseSoulLevel + i);
        }
        return totalCost;
    }

    public int GetNextSinglePointCost() => PlayerInfo.CalculateRequiredSouls(PreviewSoulLevel);

    public bool CanAffordNextPoint() => BaseSouls >= (CalculateTotalSoulCost() + GetNextSinglePointCost());

    public void ResetPending()
    {
        PendingVigor = 0;
        PendingEndurance = 0;
        PendingStrength = 0;
        PendingDexterity = 0;
    }

    public void Reset() => ResetPending();
}