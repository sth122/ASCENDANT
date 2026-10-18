using UnityEngine;

public class DataManager : MonoSingleton<DataManager>
{
    [SerializeField] public PlayerStatSO _playerStatSO;

    public EnemyStatSO _enemyStatSO; 
    public EnemyAttackDatabaseSO _enemyAttackDatabaseSO;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void Init()
    {
        if (_playerStatSO != null)
        {
            _playerStatSO.InitializeDictionary();
        }
    }
}
