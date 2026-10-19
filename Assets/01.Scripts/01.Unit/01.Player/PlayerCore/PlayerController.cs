using UnityEngine;
using Util = DebugLogger<PlayerController>;

/// <summary>
/// Player 메인 컨트롤러 컴포넌트
/// PlayerStateMachine, StatController를 중계하는 Center Controller
/// Player의 입력 버퍼 소비 및 Action Event를 제공
/// </summary>
public class PlayerController : UnitController<PlayerController>, IDamageable
{
    [Header("Runtime PlayerStat")]
    [SerializeField] private PlayerStat _runtimePlayerStat;

    [Header("Lock-On Setting")]
    [SerializeField] private float _lockOnRadius = 15f;
    [SerializeField] private float _riposteRange = 2.2f; // 앞잡 유효 사거리

    [Header("Lock-On Setting")]
    [SerializeField] private LayerMask _lockOnObstacleLayer;

    private readonly Collider[] _lockOnHitBuffer = new Collider[8];

    #region Properties
    public PlayerMovement Movement { get; private set; }
    public PlayerInputReader InputReader { get; private set; }
    public PlayerStatController PlayerStatController => _statController as PlayerStatController;
    public PlayerAnimController AnimationController { get; private set; }
    public PlayerCombatModule CombatModule { get; private set; }

    // 락온 타켓 추적용 변수 ( 추후 타켓팅 시스템과 연동 예정 )
    public Transform CurrentLockOnTarget { get; private set; }
    public Vector3 CurrentMoveDirection { get; private set; }
    #endregion

    public PlayerStat GetPlayerStat() => _runtimePlayerStat;

    protected override void Awake()
    {
        base.Awake();
        AnimationController = GetComponent<PlayerAnimController>();

        CombatModule = new PlayerCombatModule(this, AnimationController.WeaponHitbox);

        InputReader = new PlayerInputReader();
        SubscribeInputEvents();

        // 메인 카메라는 CinemachineBrain에 의해 항상 활성화된 가상 카메라의 위치/각도를 반영함
        Transform mainCameraTransform = Camera.main != null ? Camera.main.transform : null;
        Movement = new PlayerMovement(_rigidbody, transform, mainCameraTransform, _groundLayer);
    }

    /// <summary>
    /// DataManager 데이터 초기화 순서 문제로 임의로 Start 함수에 배포
    /// </summary>
    private void Start()
    {
        InitializeStatData();

        _stateMachine = new PlayerStateMachine();
        Init();
    }
    /// <summary>
    /// DataManager로부터 유닛 스탯 데이터를 조회하고 런타임 복제본을 생성하여 주입
    /// </summary>
    private void InitializeStatData()
    {
        if (DataManager.Instance != null && DataManager.Instance._playerStatSO != null)
        {
            if (DataManager.Instance._playerStatSO.TryGetData(_unitId, out PlayerStat stat))
            {
                _runtimePlayerStat = (PlayerStat)stat.Clone();
                PlayerStatController?.Init(_runtimePlayerStat);
            }
            else
            {
                Util.LogError($"{gameObject.name}: UnitId ({_unitId}) 데이터를 찾을 수 없습니다.");
            }
        }
    }
    private void OnDestroy()
    {
        UnsubscribeInputEvents();
        InputReader?.Dispose();

        if (StatController != null)
        {

            StatController.OnDieEvent -= HandleUnitDie;
            StatController.OnPoiseBreakEvent -= HandlePoiseBreak;
        }
    }

    public override void Init()
    {
        base.Init();
        if (StatController != null)
        {
            StatController.OnDieEvent -= HandleUnitDie;
            StatController.OnPoiseBreakEvent -= HandlePoiseBreak;
            StatController.OnDieEvent += HandleUnitDie;
            StatController.OnPoiseBreakEvent += HandlePoiseBreak;
        }
    }

    private void SubscribeInputEvents()
    {
        if (InputReader != null)
        {
            InputReader.OnLockOnEvent -= HandleLockOnInput;
            InputReader.OnLockOnEvent += HandleLockOnInput;
        }
    }

    private void UnsubscribeInputEvents()
    {
        if (InputReader != null)
        {
            InputReader.OnLockOnEvent -= HandleLockOnInput;
        }
    }

    protected override void InitializeStateMachine()
    {
        if (_stateMachine != null && _stateMachine is PlayerStateMachine playerSM)
        {
            playerSM.SetUpPlayerState(this);
            playerSM.Initialize(UnitState.Idle);
        }
    }

    protected override void Update()
    {
        Movement.CheckGrounded();

        // 1. 일반 이동 벡터 연산 ( 카메라 각도 기준 )
        CurrentMoveDirection = Movement.CalculateCameraRelativeDirection(InputReader.MoveInput);

        // 2. Lock-On 타켓 유실 자동 해제 검사
        ValidateCurrentLockOnTarget();

        // 3. Lock-On 상태 회전 보정 (단, ParryState 상태가 아닐 때만 적용)
        if (!(_stateMachine.CurrentState is PlayerParryState))
        {
            if (CameraManager.Instance != null && CameraManager.Instance.IsLockOnMode && CurrentLockOnTarget != null)
            {
                Vector3 lookDirection = CurrentLockOnTarget.position - transform.position;
                lookDirection.y = 0f;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    Movement.RotateTowards(lookDirection.normalized, _runtimePlayerStat.RotationSpeed, Time.deltaTime);
                }
            }
        }

        base.Update();
    }

    #region PlayerMovement Facade API
    public void MoveAPI(Vector3 direction, float speed) => Movement.Move(direction, speed);
    public void StopMovementAPI() => Movement.StopMovement();
    public void ApplyRollAPI(Vector3 rollDirection, float speed, float normalizedTime) => Movement.ApplyRoll(rollDirection, speed, normalizedTime);
    public void ApplyJumpAPI(float jumpForce) => Movement.ApplyJump(jumpForce);
    public void RotateTowardsAPI(Vector3 targetDirection, float rotationSpeed, float deltaTime)
        => Movement.RotateTowards(targetDirection, rotationSpeed, deltaTime);
    #endregion

    #region IDamageable 피격/가드 수신부
    public void ApplyDamage(in DamageContext context)
    {
        // 1. 사망 판정 데미지 무시
        if (_stateMachine.CurrentState is PlayerDieState) return;

        // 2. 무적 판정 중(구르기 등)에는 데미지 무시
        if (_stateMachine.CurrentState is PlayerRollState rollState && rollState.IsInvincible) return;

        // 3. 처형 상태 중에는 데미지 무시
        if (_stateMachine.CurrentState is PlayerRiposteState) return;

        // 4. 가드/패리 상태인 경우 분기 연산
        if (_stateMachine.CurrentState is PlayerParryState parryState)
        {
            bool isBlocked = parryState.TryProcessBlockOrParry(context, out bool parrySuccess);
            if (isBlocked) return;
        }

        // 4. 일반 피격 처리
        StatController.TakeDamage(context.Damage, context.PoiseDamage);

        if (StatController.CurrentHp > 0f)
        {
            // 슈퍼아머: 강인도가 남아있으면 공격/스킬 모션 유지, 고갈 시에만 경직 상태로 전이
            if (StatController.IsPoiseBroken)
            {
                // 연속 피격 경직 씹힘 방지
                _stateMachine.ChangeState((int)UnitState.GetHit, true);
            }
        }
    }
    #endregion

    /// <summary>
    /// 공격 키 입력 시 전방의 그로기(Poise Broken) 적을 검사하여 앞잡(Riposte) 또는 일반 공격 분기
    /// </summary>
    public bool TryTriggerRiposte()
    {
        // 공중 상태에서는 발동 차단
        if(!Movement.IsGrounded) return false;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _riposteRange, _lockOnHitBuffer, _targetLayer);
        if (hitCount == 0) return false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _lockOnHitBuffer[i];
            if (col == null || col.gameObject == gameObject) continue;

            // 적 본체 또는 부모 객체에서 UnitStatController 탐색
            UnitStatController enemyStat = col.GetComponentInParent<UnitStatController>();
            if (enemyStat != null && enemyStat.IsPoiseBroken && !enemyStat.IsDead)
            {
                // 적과 마주보고 있는지 수평 방향 벡터 내적(Dot Product) 검사
                Vector3 dirToEnemy = enemyStat.transform.position - transform.position;
                dirToEnemy.y = 0f;

                if (dirToEnemy.sqrMagnitude > 0.001f)
                {
                    dirToEnemy.Normalize();
                    float dot = Vector3.Dot(transform.forward, dirToEnemy);

                    // 플레이어 전방 약 70도 이내에 위치하면 앞잡 발동 승인
                    if (dot > 0.55f)
                    {
                        if (_stateMachine is PlayerStateMachine playerSM)
                        {
                            // 1. Riposte 상태로 전이
                            playerSM.ChangeState(UnitState.Riposte);

                            // 2. 처형 대상 적 트랜스폼 주입
                            if (playerSM.CurrentState is PlayerRiposteState riposteState)
                            {
                                riposteState.SetRiposteTarget(enemyStat.transform, enemyStat);
                            }

                            System.Array.Clear(_lockOnHitBuffer, 0, hitCount);
                            return true;
                        }
                    }
                }
            }
        }

        System.Array.Clear(_lockOnHitBuffer, 0, hitCount);
        return false;
    }

    /// <summary>
    /// Z 입력 시 실행되는 핸들러
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
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _lockOnRadius, _lockOnHitBuffer, _targetLayer);

        Transform closest = null;
        float maxSrcore = float.MaxValue;

        Transform camTransform = Camera.main != null ? Camera.main.transform : transform;

        for (int i = 0; i < hitCount; i++)
        {
            Transform target = _lockOnHitBuffer[i].transform;
            if (target == transform) continue;

            UnitStatController targetStat = target.GetComponentInParent<UnitStatController>();
            if (targetStat != null && targetStat.IsDead) continue;

            Vector3 toTarget = target.position - transform.position;
            //toTarget.y = 0f;
            float distance = toTarget.magnitude;

            // 시야 차폐 검사
            if (Physics.Linecast(transform.position + Vector3.up * 1.5f, target.position + Vector3.up * 1.0f,
                _lockOnObstacleLayer)) continue;

            // 카메라 정면 각도 가중치 부여
            Vector3 camToTargetDir = (target.position - camTransform.position).normalized;
            float dot = Vector3.Dot(camTransform.forward, camToTargetDir);

            float score = (dot * 2.0f) - (distance / _lockOnRadius);

            if (score > maxSrcore)
            {
                maxSrcore = score;
                closest = targetStat != null ? targetStat.transform : target;
            }
        }

        System.Array.Clear(_lockOnHitBuffer, 0, hitCount);
        return closest;
    }

    private void ValidateCurrentLockOnTarget()
    {
        if (CurrentLockOnTarget == null) return;

        UnitStatController targetStat = CurrentLockOnTarget.GetComponentInParent<UnitStatController>();
        if (targetStat != null && targetStat.IsDead)
        {
            ToggleLockOn(null);
            return;
        }

        float distance = Vector3.Distance(transform.position, CurrentLockOnTarget.position);
        if (distance > _lockOnRadius * 1.3f)
        {
            ToggleLockOn(null);
            return;
        }

        if (Physics.Linecast(transform.position + Vector3.up * 1.5f, CurrentLockOnTarget.position + Vector3.up * 1.0f, _lockOnObstacleLayer))
        {
            ToggleLockOn(null);
        }
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

    private void HandleUnitDie() => _stateMachine.ChangeState((int)UnitState.Die);
    private void HandlePoiseBreak() => _stateMachine.ChangeState((int)UnitState.Stun);

#if UNITY_EDITOR
    /// <summary>
    /// 유니티 에디터 플레이 중 인스펙터에서 RuntimePlayerStat 수치를 변경할 때 즉각 런타임에 동기화
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying && PlayerStatController != null && _runtimePlayerStat != null)
        {
            PlayerStatController.SyncRuntimeStats(_runtimePlayerStat);
            Util.Log("인스펙터 스탯이 런타임 모듈에 실시간 동기화");
        }
    }
#endif
    private void OnDrawGizmosSelected()
    {
        Movement?.DrawGizmos();

        // 락온 탐색 반경 기즈모 표시 (노란색 와이어 구체)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _lockOnRadius);
    }
}
