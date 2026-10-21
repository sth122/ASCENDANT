using UnityEngine;

public class DataManager : MonoSingleton<DataManager>
{
    [SerializeField] public PlayerStatusSO _playerStatusSO;

    public EnemyStatusSO _enemyStatusSO; 
    public EnemyAttackDatabaseSO _enemyAttackDatabaseSO;

    public override void Init()
    {
        if (_playerStatusSO != null)
        {
            _playerStatusSO.InitializeDictionary();
        }
        if (_enemyStatusSO != null)
        {
            _enemyStatusSO.InitializeDictionary();
        }

        if (_enemyAttackDatabaseSO != null)
        {
            _enemyAttackDatabaseSO.InitDatabase();
        }
    }
}
