using UnityEngine;
using Util = DebugLogger<PlayerController>;

public class PlayerController : UnitController<PlayerController>
{
    [Header("Ground Check")]
    [SerializeField] private LayerMask _groundLayer;

    [Header("Lock-On Setting")]
    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private float _lockOnRadius = 15f;

    public PlayerMovement Movement { get; private set; }
    public PlayerInputReader InputReader { get; private set; }
    public Vector3 CurrentMoveDirection { get; private set; }
    public PlayerStat RuntimePlayerStat { get; private set; }

    // 락온 타켓 추적용 변수 ( 추후 타켓팅 시스템과 연동 예정 )
    public Transform CurrentLockOnTarget { get; private set; }

    public PlayerStatController PlayerStatController => _statController as PlayerStatController;

    public event System.Action<InputCommandType> OnCommandTriggered;

    protected override void Awake()
    {
        base.Awake();
        
        InputReader = new PlayerInputReader();
        SubscribeInputEvents();

        // 메인 카메라는 CinemachineBrain에 의해 항상 활성화된 가상 카메라의 위치/각도를 반영함
        Transform mainCameraTransform = Camera.main != null ? Camera.main.transform : null;
        Movement = new PlayerMovement(_rigidbody, transform, mainCameraTransform, _groundLayer);

        if(DataManager.Instance != null && DataManager.Instance._playerStatSO.TryGetPlayerStats(unitId, out PlayerStat stat))
        {
            RuntimePlayerStat = (PlayerStat)stat.Clone();
            PlayerStatController.Init(RuntimePlayerStat);
        }
        else
        {
            Util.LogError($"{gameObject.name}: PlayerStat SO 데이터를 찾을 수 없습니다.");
        }

        _stateMachine = new PlayerStateMachine();
        Init();
    }

    private void OnDestroy()
    {
        UnsubscribeInputEvents();
        InputReader?.Dispose();
    }

    private void SubscribeInputEvents()
    {
        if (InputReader != null)
        {
            InputReader.OnLockOnEvent += HandleLockOnInput;
            InputReader.OnCommandInputEvent += HandleCommandInput;
        }
    }

    private void UnsubscribeInputEvents()
    {
        if (InputReader != null)
        {
            InputReader.OnLockOnEvent -= HandleLockOnInput;
            InputReader.OnCommandInputEvent -= HandleCommandInput;
        }
    }

    private void HandleCommandInput(InputCommandType command)
    {
        OnCommandTriggered?.Invoke(command);
    }

    protected override void InitializeStateMachine()
    {
        if(_stateMachine != null && _stateMachine is PlayerStateMachine playerSM)
        {
            playerSM.SetUpPlayerState(this);
            playerSM.Initialize(UnitState.Idle);
        }
    }

    protected override void Update()
    {
        Movement.CheckGounded();

        // 1. 일반 이동 벡터 연산 ( 카메라 각도 기준 )
        CurrentMoveDirection = Movement.CalculateCameraRelativeDirection(InputReader.MoveInput);

        // 2. 만약 Lock-On 상태라면, 이동과 상관없이 몸을 항상 타켓 방향으로 고정 회전
        if(CameraManager.Instance != null && CameraManager.Instance.IsLockOnMode && CurrentLockOnTarget != null)
        {
            Vector3 lookDirection = (CurrentLockOnTarget.position - transform.position);
            lookDirection.y = 0f;
            Movement.RotateTowards(lookDirection.normalized, RuntimePlayerStat.RotationSpeed, Time.deltaTime);
        }

        base.Update();
    }


    /// <summary>
    /// 마우스 휠 또는 패드 R스틱 클릭 시 실행되는 핸들러
    /// </summary>
    private void HandleLockOnInput()
    {
        // 1. 이미 락온 중이라면 락온 해제
        if (CurrentLockOnTarget != null)
        {
            ToggleLockOn(null);
            return;
        }

        // 2. 락온 대상이 없다면 주변에서 가장 가까운 적 탐색
        Transform nearestEnemy = FindNearestEnemy();
        if (nearestEnemy != null)
        {
            ToggleLockOn(nearestEnemy);
        }
    }

    /// <summary>
    /// 주변 반경 내 적 콜라이더 검출 후 최단거리 적 반환
    /// </summary>
    private Transform FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _lockOnRadius, _enemyLayer);
        Transform closest = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            float dist = Vector3.Distance(transform.position, hits[i].transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = hits[i].transform;
            }
        }

        return closest;
    }

    public void ToggleLockOn(Transform target)
    {
        CurrentLockOnTarget = target;

        if (CameraManager.Instance != null)
        {
            if (CurrentLockOnTarget != null)
            {
                CameraManager.Instance.EnableLockOn(CurrentLockOnTarget);
            }
            else
            {
                CameraManager.Instance.ResetToFreeLook();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Movement?.DrawGizmos();

        // 락온 탐색 반경 기즈모 표시 (노란색 와이어 구체)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _lockOnRadius);
    }
}
