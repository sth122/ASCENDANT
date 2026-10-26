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
        ServiceLocator.Register<IEnemyManager>(enemyManager, ServiceLifetime.Local);
    }

    protected override async Cysharp.Threading.Tasks.UniTask PreloadSceneAssetsAsync() => await Cysharp.Threading.Tasks.UniTask.CompletedTask;

    protected override async Cysharp.Threading.Tasks.UniTask SetupSceneActorsAsync()
    {
        UnityEngine.Vector3 spawnPos;
        UnityEngine.Quaternion spawnRot;

        SaveData save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSaveData : null;
        // 1-1. 새 게임이거나 체크 포인트(화톳불)에 들르지 않은 경우
        if (save == null || !save.HasCheckpoint)
        {
            spawnPos = _playerSpawnPoint != null ? _playerSpawnPoint.position : UnityEngine.Vector3.zero;
            spawnRot = _playerSpawnPoint != null ? _playerSpawnPoint.rotation : UnityEngine.Quaternion.identity;
        }
        // 1-2. 체크 포인트가 존재하는 경우
        else
        {
            // SaveData의 SerializableVector3와 Y축 회전각 복원
            spawnPos = save.LastBonfirePosition.ToVector3();
            spawnRot = UnityEngine.Quaternion.Euler(0f, save.LastBonfireRotationY, 0f);
        }

        // 2. Player 프리팹 비동기 인스턴스
        UnityEngine.GameObject playerGo = await AssetsManager.Instance.InstantiateAsync("PlayerPrefab", ct: this.destroyCancellationToken);
        if (playerGo == null) return;

        playerGo.name = "Player";
        playerGo.transform.SetPositionAndRotation(spawnPos, spawnRot);

        UnityEngine.Rigidbody rb = playerGo.GetComponent<UnityEngine.Rigidbody>();
        if (rb != null)
        {
            rb.position = spawnPos;
            rb.rotation = spawnRot;
            rb.linearVelocity = UnityEngine.Vector3.zero;
            rb.angularVelocity = UnityEngine.Vector3.zero;
        }

        UnityEngine.Physics.SyncTransforms();

        _spawnedPlayer = playerGo.GetComponent<PlayerController>();

        SetupPlayerRuntimeStatus(_spawnedPlayer, save);

        // 4. 시네마신 가상 카메라 타깃 주입
        if (CameraManager.Instance != null && _spawnedPlayer != null)
        {
            UnityEngine.Transform camTarget = _spawnedPlayer.CameraTarget != null ? _spawnedPlayer.CameraTarget : _spawnedPlayer.transform;
            CameraManager.Instance.RegisterSceneCameras(_sceneFreeLookCamera, _sceneLockOnCamera);
            CameraManager.Instance.SetPlayerTarget(camTarget);
            Util.Log($"[CameraManager] 타깃 주입 완료: {camTarget.name}");
        }
    }

    /// <summary>
    /// PlayerStatusSO의 불변 템플릿에 세이브 파일의 수치를 융합하여 PlayerController에 주입
    /// </summary>
    private void SetupPlayerRuntimeStatus(PlayerController player, SaveData save)
    {
        if (player == null) return;

        // DataManager에서 정적 기획 원형 SO 조회
        if (!DataManager.Instance.TryGetSingle<PlayerStatusSO>(out PlayerStatusSO statusSO))
        {
            Util.LogError("DataManager에서 PlayerStatusSO를 찾을 수 없습니다.");
            return;
        }

        // 기본 템플릿 복제 (이동속도, 회전속도, 애니메이션 계수 등 확보)
        if (!statusSO.TryGetData(UnitId.Player, out var status))
        {
            Util.LogError("PlayerStatusSO에서 PlayerStatus를 찾을 수 없습니다.");
            return;
        }
        PlayerStatus runtimeStatus = (PlayerStatus)status.Clone();

        float initialHp;

        // 세이브 데이터가 존재한다면 성장 스탯과 체력 복원
        if (save != null && save.HasCheckpoint && save.PlayerData != null)
        {
            runtimeStatus.ApplySaveData(save.PlayerData);

            // 새 게임인 경우 풀피, 이어하기인 경우 세이브된 체력 적용
            initialHp = save.PlayerData.CurrentHp;
        }
        else
        {
            initialHp = runtimeStatus.CalculateMaxHp();
            if (save != null && save.PlayerData != null)
            {
                save.PlayerData = new PlayerSaveData(runtimeStatus, initialHp);
                SaveManager.Instance.SaveCurrentGame(save.SaveSlotIndex);
            }
        }

        // PlayerController 초기화 실행 (상태머신 생성, HP 주입, FSM 기동)
        player.InitializePlayerData(runtimeStatus, initialHp);
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
        // 1. 거시적 게임 상태를 Playing으로 전환
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameState.Playing);
        }

        // 2. 조작권 활성화 및 커서 잠금
        if (UIManager.Instance != null && UIManager.Instance.InputReader != null)
        {
            UIManager.Instance.InputReader.EnablePlayerInput();
        }

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