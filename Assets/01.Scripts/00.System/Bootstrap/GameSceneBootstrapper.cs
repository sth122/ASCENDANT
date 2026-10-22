using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameSceneBootstrapper : SceneBootstrapper, IPlayerProvider
{
    private PlayerController _spawnedPlayer;

    public PlayerController Player => _spawnedPlayer;
    public Transform PlayerTransform => _spawnedPlayer != null ? _spawnedPlayer.transform : null;
    public bool IsPlayerSpawned => _spawnedPlayer != null;

    protected override void RegisterSceneServices()
    {
        ServiceLocator.Register<IPlayerProvider>(this, ServiceLifetime.Local);
    }

    protected override async UniTask PreloadSceneAssetsAsync()
    {
        var ct = this.destroyCancellationToken;
        await ObjectPoolManager.Instance.PreloadPoolAsync(PoolType.HitVFX_Slash, "HitVFX_Slash", 10, ct);
        await ObjectPoolManager.Instance.PreloadPoolAsync(PoolType.EnemyFloatingHpBar, "FloatingHpBar", 5, ct);
    }

    protected override async UniTask SetupSceneActorsAsync()
    {
        var ct = this.destroyCancellationToken;
        GameObject pGo = await AssetsManager.Instance.InstantiateAsync("PlayerPrefab", ct: ct);
        if (pGo != null && _playerSpawnPoint != null)
        {
            pGo.transform.position = _playerSpawnPoint.position;
            pGo.transform.rotation = _playerSpawnPoint.rotation;
            _spawnedPlayer = pGo.GetComponent<PlayerController>();
        }
    }

    protected override async UniTask SetupSceneUIAsync()
    {
        await UIManager.Instance.OpenUIAsync<PlayerHUDView, PlayerHUDPresetner, PlayerHUDModel>(
            UIId.PlayerHUD, "PlayerHUD", ct: this.destroyCancellationToken
        );
    }

    protected override void OnSceneReadyCompleted()
    {
        _spawnedPlayer?.InputReader.EnablePlayerInput();
    }
}