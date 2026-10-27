/// <summary>
/// 플레이어의 영속적 스탯 및 진행 상태를 저장하는 직렬화 데이터 클래스
/// </summary>
[System.Serializable]
public class PlayerSaveData
{
    [UnityEngine.Header("Level & Souls")]
    public int SoulLevel = 1;
    public int CurrentSouls = 0;

    [UnityEngine.Header("Attributes")]
    public int Vigor = 10;
    public int Endurance = 10;
    public int Strength = 10;
    public int Dexterity = 10;

    [UnityEngine.Header("Runtime Status")]
    public float CurrentHp = 650f; // Vigor 10 기준 기본 최대 HP

    public PlayerSaveData() { }

    public PlayerSaveData(PlayerStatus status, float currentHp)
    {
        if (status == null) return;

        SoulLevel = status.SoulLevel;
        CurrentSouls = status.CurrentSouls;
        Vigor = status.Vigor;
        Endurance = status.Endurance;
        Strength = status.Strength;
        Dexterity = status.Dexterity;
        CurrentHp = currentHp;
    }
}