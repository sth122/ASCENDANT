using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.Cinemachine;
using Util = DebugLogger<BossSceneBootstrapper>;

public class BossSceneBootstrapper : SceneBootstrapper, IPlayerProvider
{
    [Header("Boss Scene Camera Rig")]
    [SerializeField] private CinemachineCamera _sceneFreeLookCamera;
    [SerializeField] private CinemachineCamera _sceneLockOnCamera;
    [SerializeField] private CinemachineCamera _bossCutsceneCamera;

    [Header("Boss & Weapon Fallback")]
    [SerializeField] private WeaponItemData _fallbackWeaponData;

    private PlayerController _spawnedPlayer;

    public PlayerController Player => _spawnedPlayer;
    public Transform PlayerTransform => _spawnedPlayer != null ? _spawnedPlayer.transform : null;
    public bool IsPlayerSpawned => _spawnedPlayer != null;

    protected override void RegisterSceneServices()
    {
        ServiceLocator.Register<IPlayerProvider>(this, ServiceLifetime.Local);

        EnemyManager enemyManager = GetComponent<EnemyManager>();
        if (enemyManager == null)
        {
            enemyManager = gameObject.AddComponent<EnemyManager>();
        }
        ServiceLocator.Register<IEnemyManager>(enemyManager, ServiceLifetime.Local);
    }

    protected override async UniTask PreloadSceneAssetsAsync() => await UniTask.CompletedTask;

    protected override async UniTask SetupSceneActorsAsync()
    {
        // 보스전은 화톳불 좌표가 아닌 보스룸 전용 스폰 포인트를 강제 사용
        Vector3 spawnPos = _playerSpawnPoint != null ? _playerSpawnPoint.position : Vector3.zero;
        Quaternion spawnRot = _playerSpawnPoint != null ? _playerSpawnPoint.rotation : Quaternion.identity;

        GameObject playerGo = await AssetsManager.Instance.InstantiateAsync("PlayerPrefab", ct: this.destroyCancellationToken);
        if (playerGo == null)
        {
            Util.LogError("PlayerPrefab 로드 실패");
            return;
        }

        playerGo.name = "Player";
        playerGo.transform.SetPositionAndRotation(spawnPos, spawnRot);

        if (playerGo.TryGetComponent(out Rigidbody rb))
        {
            rb.position = spawnPos;
            rb.rotation = spawnRot;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
        _spawnedPlayer = playerGo.GetComponent<PlayerController>();

        SetupPlayerRuntimeStatus(_spawnedPlayer);

        // 무기 데이터 바인딩 보장 (인벤토리 데이터 미동기화 시 폴백)
        if (_spawnedPlayer.CombatModule != null && _spawnedPlayer.CombatModule.CurrentWeaponData == null && _fallbackWeaponData != null)
        {
            _spawnedPlayer.CombatModule.SetWeaponData(_fallbackWeaponData);
        }

        // 보스 컷신 카메라를 포함하여 3종 카메라 등록
        if (CameraManager.Instance != null && _spawnedPlayer != null)
        {
            Transform camTarget = _spawnedPlayer.CameraTarget != null ? _spawnedPlayer.CameraTarget : _spawnedPlayer.transform;
            CameraManager.Instance.RegisterSceneCameras(_sceneFreeLookCamera, _sceneLockOnCamera, _bossCutsceneCamera);
            CameraManager.Instance.SetPlayerTarget(camTarget);
        }
    }

    private void SetupPlayerRuntimeStatus(PlayerController player)
    {
        if (player == null) return;

        if (!DataManager.Instance.TryGetSingle<PlayerStatusSO>(out PlayerStatusSO statusSO) ||
            !statusSO.TryGetData(UnitId.Player, out var status))
        {
            Util.LogError("PlayerStatusSO 조회 실패");
            return;
        }

        PlayerInfo runtimeStatus = (PlayerInfo)status.Clone();
        float initialHp;

        if (SessionDataManager.Instance.HasActiveSession)
        {
            PlayerSaveData sessionData = SessionDataManager.Instance.CurrentSessionData;
            runtimeStatus.ApplySaveData(sessionData);
            initialHp = sessionData.CurrentHp;
        }
        else
        {
            initialHp = runtimeStatus.CalculateMaxHp();
            SessionDataManager.Instance.InitializeNewSession(runtimeStatus);
        }

        player.InitializePlayerData(runtimeStatus, initialHp);
    }

    protected override async UniTask SetupSceneUIAsync()
    {
        PlayerHUDModel hudModel = new PlayerHUDModel();
        await UIManager.Instance.OpenUIAsync<PlayerHUDView, PlayerHUDPresenter, PlayerHUDModel>(
            UIId.PlayerHUD, hudModel, ct: this.destroyCancellationToken
        );
    }

    protected override void OnSceneReadyCompleted()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameState.Playing);
        }

        if (UIManager.Instance != null && UIManager.Instance.InputReader != null)
        {
            UIManager.Instance.InputReader.EnablePlayerInput();
        }

        if (_spawnedPlayer != null && _spawnedPlayer.InputReader != null)
        {
            _spawnedPlayer.InputReader.EnablePlayerInput();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 카메라 즉시 스냅 (2초간 날아가는 현상 차단)
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SnapToFreeLook();
        }

        EventBusSystem.Publish(new GameSceneReadyEvent(_sceneId));
    }

    protected override void OnDestroy()
    {
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.UnregisterSceneCameras();
        }
        base.OnDestroy();
    }
}