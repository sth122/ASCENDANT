using Util = DebugLogger<GameSceneBootstrapper>;
/// <summary>
/// RPG가 진행되는 Game Scene 전용 Bootstrapper
/// </summary>
public class GameSceneBootstrapper : SceneBootstrapper, IPlayerProvider
{
    [UnityEngine.Header("Scene Camera Rig")]
    [UnityEngine.SerializeField] private Unity.Cinemachine.CinemachineCamera _sceneFreeLookCamera;
    [UnityEngine.SerializeField] private Unity.Cinemachine.CinemachineCamera _sceneLockOnCamera;

    private PlayerController _spawnedPlayer;

    #region IPlayerProvider 구현부
    public PlayerController Player => _spawnedPlayer;
    public UnityEngine.Transform PlayerTransform => _spawnedPlayer != null ? _spawnedPlayer.transform : null;
    public bool IsPlayerSpawned => _spawnedPlayer != null;
    #endregion

    protected override void RegisterSceneServices()
    {
        ServiceLocator.Register<IPlayerProvider>(this, ServiceLifetime.Local);
        EnemyManager enemyManager = GetComponent<EnemyManager>();
        if (enemyManager == null)
        {
            enemyManager = gameObject.AddComponent<EnemyManager>();
        }
    }

    protected override async Cysharp.Threading.Tasks.UniTask PreloadSceneAssetsAsync()
    {
        await Cysharp.Threading.Tasks.UniTask.CompletedTask;
    }

    protected override async Cysharp.Threading.Tasks.UniTask SetupSceneActorsAsync()
    {
        var ct = this.destroyCancellationToken;

        // 1. 씬 가상 카메라들을 CameraManager에 먼저 등록 - 씬 이동 시 카메라 추적 문제 해결 방안
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.RegisterSceneCameras(_sceneFreeLookCamera, _sceneLockOnCamera);
        }

        // 2. Player 프리팹 비동기 인스턴스
        UnityEngine.GameObject playerGo = await AssetsManager.Instance.InstantiateAsync("PlayerPrefab", ct: ct);
        if (playerGo == null) return;

        _spawnedPlayer = playerGo.GetComponent<PlayerController>();

        // 3. 스폰 위치 결정 (세이브 데이터가 존재하면 마지막 체크포인트 위치 우선)
        UnityEngine.Vector3 spawnPosition = _playerSpawnPoint != null ? _playerSpawnPoint.position : UnityEngine.Vector3.zero;
        UnityEngine.Quaternion spawnRotation = _playerSpawnPoint != null ? _playerSpawnPoint.rotation : UnityEngine.Quaternion.identity;

        PlayerSaveData savedData = null;
        if (SaveManager.Instance != null && SaveManager.Instance.CurrentSaveData != null)
        {
            SaveData save = SaveManager.Instance.CurrentSaveData;
            UnityEngine.Vector3 savedPos = save.LastBonfirePosition.ToVector3();

            // 저장된 좌표가 기본 0이 아니거나 유효하면 해당 위치로 스폰
            if (savedPos.sqrMagnitude > 0.001f)
            {
                spawnPosition = savedPos;
                spawnRotation = UnityEngine.Quaternion.Euler(0f, save.LastBonfireRotationY, 0f);
            }

            // 3. 저장된 체력/스탯 데이터 복원
            savedData = save.PlayerData;
        }

        playerGo.transform.position = spawnPosition;
        playerGo.transform.rotation = spawnRotation;

        // 4. DataManager에서 베이스 스탯 조회 후 Player 초기화 파이프라인 실행
        PlayerStatus runtimeStat = null;
        if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<PlayerStatusSO>(out PlayerStatusSO _playerStatusSO))
        {
            if (_playerStatusSO.TryGetData(UnitId.Player, out PlayerStatus statusSO))
            {
                runtimeStat = (PlayerStatus)statusSO.Clone();
            }
        }

        float initialHp = savedData != null ? savedData.CurrentHp : -1f;
        _spawnedPlayer.InitializePlayerData(runtimeStat, initialHp);

        // 5. 시네마신 카메라에 스폰된 플레이어 트랜스폼 자동 바인딩
        if (CameraManager.Instance != null && _spawnedPlayer != null)
        {
            CameraManager.Instance.SetPlayerTarget(_spawnedPlayer.transform);
        }
    }

    protected override async Cysharp.Threading.Tasks.UniTask SetupSceneUIAsync()
    {
        PlayerHUDModel hudModel = new PlayerHUDModel();
        await UIManager.Instance.OpenUIAsync<PlayerHUDView, PlayerHUDPresetner, PlayerHUDModel>(
            UIId.PlayerHUD, "PlayerHUD", hudModel, ct: this.destroyCancellationToken
        );
    }

    protected override void OnSceneReadyCompleted()
    {
        // 1. UIManager의 잔여 UI 입력 맵 해제
        if (UIManager.Instance != null && UIManager.Instance.InputReader != null)
        {
            UIManager.Instance.InputReader.EnablePlayerInput();
        }

        // 2. 플레이어 조작 맵 활성화
        if (_spawnedPlayer != null && _spawnedPlayer.InputReader != null)
        {
            _spawnedPlayer.InputReader.EnablePlayerInput();
        }

        UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;

        Util.Log("[GameScene 부트스트랩 완료: 인게임 조작 개시]");
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