using UnityEngine;
using Util = DebugLogger<PlayerController>;

/// <summary>
/// Player 메인 컨트롤러 컴포넌트
/// PlayerStateMachine, StatController를 중계하는 Center Controller
/// Player의 입력 버퍼 소비 및 Action Event를 제공
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(PlayerStatusController))]
[RequireComponent(typeof(PlayerAnimController))]
public class PlayerController : UnitController<PlayerController>, IDamageable
{
    [Header("Lock-On Setting")]
    [SerializeField] private float _lockOnRadius = 15f;
    [SerializeField] private float _riposteRange = 2.2f; // 앞잡 유효 사거리
    [SerializeField] private LayerMask _lockOnObstacleLayer;

    [Header("Camera Anchor")]
    [SerializeField] private Transform _cameraTarget;

    private readonly Collider[] _lockOnHitBuffer = new Collider[16];

    #region Properties
    public PlayerMovement Movement { get; private set; }
    public PlayerInputReader InputReader { get; private set; }
    public PlayerStatusController StatusController => _unitStatusController as PlayerStatusController;
    public PlayerAnimController AnimationController { get; private set; }
    public PlayerCombatModule CombatModule { get; private set; }

    public Transform CameraTarget => _cameraTarget != null ? _cameraTarget : transform;

    // 락온 타켓 추적용 변수 ( 추후 타켓팅 시스템과 연동 예정 )
    public Transform CurrentLockOnTarget { get; private set; }
    public Vector3 CurrentMoveDirection { get; private set; }
    #endregion

    protected override void Awake()
    {
        base.Awake();

        AnimationController = GetComponent<PlayerAnimController>();
        InputReader = new PlayerInputReader();
        SubscribeInputEvents();

        // 메인 카메라는 CinemachineBrain에 의해 항상 활성화된 가상 카메라의 위치/각도를 반영함
        Movement = new PlayerMovement(_rigidbody, transform, CameraTarget, _groundLayer);

        CombatModule = new PlayerCombatModule(this, AnimationController.WeaponHitbox);
        EventBusSystem.Subscribe<GameStateChangedEvent>(HandleGameStateChanged);
    }

    /// <summary>
    /// DataManager로부터 유닛 스탯 데이터를 조회하고 런타임 복제본을 생성하여 주입
    /// </summary>
    public void InitializePlayerData(PlayerInfo runtimeStatus, float initialHp = -1f)
    {
        if (runtimeStatus != null && StatusController != null)
        {
            StatusController.Init(runtimeStatus);

            if (initialHp >= 0f)
            {
                StatusController.SetHpDirectly(initialHp);
            }
        }

        _stateMachine = new PlayerStateMachine();
        Init();

        // 씬 시작 시 메인 카메라 트랜스폼 캐시 갱신
        Movement.CacheMainCamera();
    }

    public override void Init()
    {
        base.Init();
        if (StatusController != null)
        {
            StatusController.OnDieEvent -= HandleUnitDie;
            StatusController.OnPoiseBreakEvent -= HandlePoiseBreak;
            StatusController.OnDieEvent += HandleUnitDie;
            StatusController.OnPoiseBreakEvent += HandlePoiseBreak;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeInputEvents();
        InputReader?.Dispose();

        if (StatusController != null)
        {

            StatusController.OnDieEvent -= HandleUnitDie;
            StatusController.OnPoiseBreakEvent -= HandlePoiseBreak;
        }
        EventBusSystem.Unsubscribe<GameStateChangedEvent>(HandleGameStateChanged);
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
        // 방어 코드. 일시정지 중에는 물리 이동/카메라/회전 연산을 일절 수행하지 않음
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            return;
        }

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
                    float rotSpeed = StatusController.PlayerStatusSource != null ? StatusController.PlayerStatusSource.RotationSpeed : 10.0f;
                    Movement.RotateTowards(lookDirection.normalized, rotSpeed, Time.deltaTime);
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

        // 5. 일반 피격 처리
        StatusController.TakeDamage(context.Damage, context.PoiseDamage);
        if(StatusController.CurrentHp <= 0f) return;

        // 6. 패링 불가 공격 적중 시 GetHit
        if(!context.IsParryable)
        {
            _stateMachine.ChangeState((int)UnitState.GetHit, true);
            return;
        }

        // 7. 공격 중일 때의 포이즈 슈퍼아머 판정
        if (_stateMachine.CurrentState is PlayerAttackState)
        {
            // 포이즈가 아직 남아있고 고갈되지 않았다면 모션 유지 (슈퍼아머 버팀)
            if (!StatusController.IsPoiseBroken && !StatusController.IsPoiseDepleted) return;

            // 포이즈가 0으로 깨지면 공격 캔슬 후 피격 경직 발생
            _stateMachine.ChangeState((int)UnitState.GetHit, true);
            return;
        }

        // 8. 비공격(대기/이동/달리기) 상태에서의 피격 판정
        // 포이즈가 한 번 깨진 후 100% 완전 회복되기 전까지는 피격 시 무조건 GetHit
        if (StatusController.IsPoiseDepleted || StatusController.IsPoiseBroken)
        {
            _stateMachine.ChangeState((int)UnitState.GetHit, true);
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
            EnemyController enemy = col.GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.CanBeRiposted && enemy.StatusController.IsPoiseBroken && !enemy.StatusController.IsDead)
            {
                Vector3 dirToEnemy = enemy.transform.position - transform.position;
                dirToEnemy.y = 0f;

                if (dirToEnemy.sqrMagnitude > 0.001f)
                {
                    dirToEnemy.Normalize();
                    float dotFacing = Vector3.Dot(transform.forward, dirToEnemy);

                    // 플레이어가 적을 바라보고 있는 각도 검사 (약 60도 이내)
                    if (dotFacing > 0.5f)
                    {
                        if (_stateMachine is PlayerStateMachine playerSM)
                        {
                            playerSM.ChangeState(UnitState.Riposte);

                            if (playerSM.CurrentState is PlayerRiposteState riposteState)
                            {
                                riposteState.SetRiposteTarget(enemy.transform, enemy.StatusController);
                            }

                            // 피격 적에게도 앞잡 피격 상태 강제 전이 호출
                            enemy.OnRiposted(transform);

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
        if (hitCount == 0) return null;

        Transform closest = null;
        float maxScore = float.MinValue; // 버그 수정: float.MaxValue -> float.MinValue

        Transform camTransform = Camera.main != null ? Camera.main.transform : transform;

        for (int i = 0; i < hitCount; i++)
        {
            Transform target = _lockOnHitBuffer[i].transform;
            if (target == transform) continue;

            UnitStatusController targetStat = target.GetComponentInParent<UnitStatusController>();
            if (targetStat != null && targetStat.IsDead) continue;

            Vector3 toTarget = target.position - transform.position;
            float distance = toTarget.magnitude;

            // 벽 차폐 검사
            if (Physics.Linecast(transform.position + Vector3.up * 1.5f, target.position + Vector3.up * 1.0f, _lockOnObstacleLayer))
                continue;

            // 카메라 정면 벡터와의 내적을 통한 시야각 가중치 연산
            Vector3 camToTargetDir = (target.position - camTransform.position).normalized;
            float dot = Vector3.Dot(camTransform.forward, camToTargetDir);

            // 카메라 등 뒤에 있는 적은 배제
            if (dot < 0.1f) continue;

            float score = (dot * 3.0f) - (distance / _lockOnRadius);

            if (score > maxScore)
            {
                maxScore = score;
                closest = targetStat != null ? targetStat.transform : target;
            }
        }

        System.Array.Clear(_lockOnHitBuffer, 0, hitCount);
        return closest;
    }

    private void ValidateCurrentLockOnTarget()
    {
        if (CurrentLockOnTarget == null) return;

        UnitStatusController targetStat = CurrentLockOnTarget.GetComponentInParent<UnitStatusController>();
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
        EventBusSystem.Publish(new LockOnTargetChangedEvent(CurrentLockOnTarget, CurrentLockOnTarget != null));

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
    private void HandlePoiseBreak() { }

    /// <summary>
    /// GameManager 상태에 따라 플레이어 액션 맵을 물리적으로 온/오프
    /// </summary>
    private void HandleGameStateChanged(GameStateChangedEvent evt)
    {
        if (evt.NewState == GameState.Paused)
        {
            // 메뉴가 열렸을 때 플레이어 입력 맵을 즉각 비활성화 (마우스 클릭 공격 방지)
            InputReader?.DisablePlayerInput();
        }
        else if (evt.NewState == GameState.Playing)
        {
            // 게임 재개 시 플레이어 입력 맵 복원
            InputReader?.EnablePlayerInput();
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
