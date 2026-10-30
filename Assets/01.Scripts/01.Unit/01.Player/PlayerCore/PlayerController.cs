using UnityEditor;
using UnityEngine;
using Util = DebugLogger<PlayerController>;

/// <summary>
/// Player 메인 컨트롤러 컴포넌트
/// PlayerStateMachine, StatController를 중계하는 Center Controller
/// Player의 입력 버퍼 소비 및 Action Event를 제공
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(PlayerStatusController))]
[RequireComponent(typeof(PlayerAnimController))]
public class PlayerController : UnitController, IDamageable
{
    [Header("Lock-On Setting")]
    [SerializeField] private float _lockOnRadius = 15f;
    [SerializeField] private float _riposteRange = 2.2f;
    [SerializeField] private LayerMask _lockOnObstacleLayer;

    [Header("Camera Anchor")]
    [SerializeField] private Transform _cameraTarget;

    [Header("Combat Hitbox Component")]
    [SerializeField] private WeaponSweepHitbox _playerWeaponHitboxTest;

    private readonly Collider[] _lockOnHitBuffer = new Collider[16];

    #region 도메인 컴포넌트 및 모듈
    protected PlayerMovement _movement;
    protected PlayerInputReader _inputReader;
    protected PlayerStatusController _statusController;
    protected PlayerAnimController _animController;
    protected PlayerCombatModule _combatModule;
    protected PlayerStateMachine _stateMachine;
    #endregion

    #region Properties
    public override UnitStatusController BaseStatusController => _statusController;
    public override UnitAnimController BaseAnimController => _animController;

    public PlayerMovement Movement => _movement;
    public PlayerInputReader InputReader => _inputReader;
    public PlayerStatusController StatusController => _statusController;
    public PlayerAnimController AnimationController => _animController;
    public PlayerCombatModule CombatModule => _combatModule;
    public PlayerStateMachine StateMachineInstance => _stateMachine;

    public Transform CameraTarget => _cameraTarget != null ? _cameraTarget : transform;
    public Transform CurrentLockOnTarget { get; private set; }
    public Vector3 CurrentMoveDirection { get; private set; }
    #endregion

    protected override void SetupComponents()
    {
        _unitId = UnitId.Player;

        _statusController = GetComponent<PlayerStatusController>();
        _animController = GetComponent<PlayerAnimController>();

        _inputReader = new PlayerInputReader();
        SubscribeInputEvents();

        if (_cameraTarget == null) _cameraTarget = transform;
        _movement = new PlayerMovement(_rigidbody, transform, CameraTarget, _groundLayer, _groundCheckConfig);

        if (_playerWeaponHitboxTest == null)
        {
            _playerWeaponHitboxTest = GetComponentInChildren<WeaponSweepHitbox>();
        }
        _combatModule = new PlayerCombatModule(this, _playerWeaponHitboxTest);

        EventBusSystem.Subscribe<GameStateChangedEvent>(HandleGameStateChanged);
    }

    /// <summary>
    /// DataManager로부터 런타임 스탯을 주입받아 초기화 수행
    /// </summary>
    public void InitializePlayerData(PlayerInfo runtimeStatus, float initialHp = -1f)
    {
        if (runtimeStatus != null && _statusController != null)
        {
            _statusController.Init(runtimeStatus);

            if (initialHp >= 0f)
            {
                _statusController.SetHpDirectly(initialHp);
            }
        }

        Init();
        _movement.CacheMainCamera();
    }

    public override void Init()
    {
        base.Init();
        InitializeStateMachine();

        if (_statusController != null)
        {
            _statusController.OnDieEvent -= HandleUnitDie;
            _statusController.OnDieEvent += HandleUnitDie;
        }
    }

    protected virtual void InitializeStateMachine()
    {
        _stateMachine = new PlayerStateMachine();
        _stateMachine.SetUpPlayerState(this);
        _stateMachine.Initialize(UnitState.Idle);
    }

    protected override void TickController()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // 지면 감지
        _movement.CheckGrounded();

        // 카메라 기준 상대 이동 벡터 계산
        CurrentMoveDirection = _movement.CalculateCameraRelativeDirection(_inputReader.MoveInput);

        // 락온 타깃 유효성 검사
        ValidateCurrentLockOnTarget();

        // FSM Update 루프 가동
        _stateMachine?.Update();
    }

    protected override void FixedTickController()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // FSM 물리 고정 프레임 틱 가동
        _stateMachine?.FixedUpdate();
    }

    #region Movement Facade API
    public void MoveAPI(Vector3 direction, float speed) => _movement.Move(direction, speed);
    public void StopMovementAPI() => _movement.StopMovement();
    public void ApplyRollAPI(Vector3 rollDirection, float speed, float normalizedTime) => _movement.ApplyRoll(rollDirection, speed, normalizedTime);
    public void ApplyJumpAPI(float jumpForce) => _movement.ApplyJump(jumpForce);
    public void RotateTowardsAPI(Vector3 targetDirection, float rotationSpeed, float deltaTime) => _movement.RotateTowards(targetDirection, rotationSpeed, deltaTime);
    #endregion

    #region IDamageableTest 피격 수신부
    public void ApplyDamage(in DamageContext context)
    {
        // 1. 사망 상태 판정
        if (_stateMachine.CurrentState is PlayerDieState) return;

        // 2. 구르기 무적 판정
        if (_stateMachine.CurrentState is PlayerRollState rollState && rollState.IsInvincible) return;

        // 3. 기상 무적 판정
        if (_stateMachine.CurrentState is PlayerKnockdownState knockdownState && knockdownState.IsInvincibleDuringWakeUp) return;

        // 4. 처형 액션 중 무적
        if (_stateMachine.CurrentState is PlayerRiposteState) return;

        // 5. 가드 및 패링 판정 분기
        if (_stateMachine.CurrentState is PlayerParryState parryState)
        {
            bool isBlocked = parryState.TryProcessBlockOrParry(in context, out bool parrySuccess);
            if (isBlocked) return;
        }

        // 6. 장판 도트 대미지 (DoT)
        if (context.ImpactType == AttackImpactType.DoT)
        {
            _statusController.TakeDamage(context.Damage, 0f);
            return;
        }

        // 7. 기본 체력 및 포이즈 차감
        _statusController.TakeDamage(context.Damage, context.PoiseDamage);
        if (_statusController.CurrentHp <= 0f) return;

        // 8. 보스 강공격 (Heavy Impact): 물리 넉백 + 2초 다운
        if (context.ImpactType == AttackImpactType.Heavy)
        {
            ApplyKnockbackImpulse(context.HitNormal, context.KnockbackForce);
            _stateMachine.ChangeState(UnitState.Stun, true);
            return;
        }

        // 9. 패링 불가 공격 적중 시 경직
        if (!context.IsParryable)
        {
            _stateMachine.ChangeState(UnitState.GetHit, true);
            return;
        }

        // 10. 공격 중 슈퍼아머 버티기 판정
        if (_stateMachine.CurrentState is PlayerAttackState)
        {
            if (!_statusController.IsPoiseBroken && !_statusController.IsPoiseDepleted) return;
            _stateMachine.ChangeState(UnitState.GetHit, true);
            return;
        }

        // 11. 비공격 상태 일반 피격 경직
        _stateMachine.ChangeState(UnitState.GetHit, true);
    }

    private void ApplyKnockbackImpulse(Vector3 hitNormal, float force)
    {
        if (_rigidbody != null && force > 0.01f)
        {
            Vector3 knockbackDir = -hitNormal;
            knockbackDir.y = 0.2f;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.AddForce(knockbackDir.normalized * force, ForceMode.Impulse);
        }
    }
    #endregion

    #region 앞잡(Riposte) 판정
    public bool TryTriggerRiposte()
    {
        if (!_movement.IsGrounded) return false;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _riposteRange, _lockOnHitBuffer, _targetLayer);
        if (hitCount == 0) return false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _lockOnHitBuffer[i];
            if (col == null || col.gameObject == gameObject) continue;

            // IRiposteable 인터페이스로 추상화
            IRiposteable riposteTarget = col.GetComponentInParent<IRiposteable>();
            if (riposteTarget != null && riposteTarget.CanBeRiposted && !riposteTarget.IsDead)
            {
                Vector3 dirToEnemy = riposteTarget.RiposteSocket.position - transform.position;
                dirToEnemy.y = 0f;

                if (dirToEnemy.sqrMagnitude > 0.001f && Vector3.Dot(transform.forward, dirToEnemy.normalized) > 0.5f)
                {
                    _stateMachine.ChangeState(UnitState.Riposte);

                    if (_stateMachine.CurrentState is PlayerRiposteState riposteState)
                    {
                        riposteState.SetRiposteTarget(riposteTarget);
                    }

                    System.Array.Clear(_lockOnHitBuffer, 0, hitCount);
                    return true;
                }
            }
        }

        System.Array.Clear(_lockOnHitBuffer, 0, hitCount);
        return false;
    }
    #endregion

    #region 락온 타겟팅
    private void SubscribeInputEvents()
    {
        if (_inputReader != null)
        {
            _inputReader.OnLockOnEvent -= HandleLockOnInput;
            _inputReader.OnLockOnEvent += HandleLockOnInput;
        }
    }

    private void UnsubscribeInputEvents()
    {
        if (_inputReader != null)
        {
            _inputReader.OnLockOnEvent -= HandleLockOnInput;
        }
    }

    private void HandleLockOnInput()
    {
        if (CurrentLockOnTarget != null)
        {
            ToggleLockOn(null);
            return;
        }

        Transform nearestEnemy = FindNearestEnemy();
        if (nearestEnemy != null)
        {
            ToggleLockOn(nearestEnemy);
        }
    }

    private Transform FindNearestEnemy()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _lockOnRadius, _lockOnHitBuffer, _targetLayer);
        if (hitCount == 0) return null;

        Transform closest = null;
        float maxScore = float.MinValue;
        Transform camTransform = Camera.main != null ? Camera.main.transform : transform;

        for (int i = 0; i < hitCount; i++)
        {
            Transform target = _lockOnHitBuffer[i].transform;
            if (target == transform) continue;

            UnitStatusController targetStat = target.GetComponentInParent<UnitStatusController>();
            if (targetStat != null && targetStat.IsDead) continue;

            Vector3 toTarget = target.position - transform.position;
            float distance = toTarget.magnitude;

            if (Physics.Linecast(transform.position + Vector3.up * 1.5f, target.position + Vector3.up * 1.0f, _lockOnObstacleLayer))
                continue;

            Vector3 camToTargetDir = (target.position - camTransform.position).normalized;
            float dot = Vector3.Dot(camTransform.forward, camToTargetDir);
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
    #endregion

    private void HandleUnitDie() => _stateMachine.ChangeState(UnitState.Die);

    private void HandleGameStateChanged(GameStateChangedEvent evt)
    {
        if (evt.NewState == GameState.Paused)
        {
            _inputReader?.DisablePlayerInput();
        }
        else if (evt.NewState == GameState.Playing)
        {
            _inputReader?.EnablePlayerInput();
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        // 에디터 플레이 모드에서 지면 감지 수치를 실시간 수정했을 때 Movement에 즉각 반영
        if (Application.isPlaying && _movement != null)
        {
            _movement.UpdateGroundConfig(_groundCheckConfig);
        }
    }
#endif

    #region 기즈모 시각화
    protected virtual void OnDrawGizmosSelected()
    {
        // 지면 판정 및 경사면 법선 기즈모 호출
        _movement?.DrawGizmos();

        Vector3 playerPos = transform.position;
        Vector3 eyePos = playerPos + Vector3.up * 1.5f;

#if UNITY_EDITOR
        // 락온 탐색 반경 (청록색)
        Handles.color = new Color(0f, 0.9f, 0.8f, 0.1f);
        Handles.DrawSolidDisc(playerPos, Vector3.up, _lockOnRadius);
        Handles.color = new Color(0f, 0.9f, 0.8f, 0.5f);
        Handles.DrawWireDisc(playerPos, Vector3.up, _lockOnRadius);

        // 앞잡(Riposte) 처형 가능 부채꼴 (주황색: 120도, 2.2m)
        Vector3 forwardDir = transform.forward;
        float riposteAngle = 120.0f;
        Vector3 leftBoundary = Quaternion.AngleAxis(-riposteAngle * 0.5f, Vector3.up) * forwardDir;

        Handles.color = new Color(1.0f, 0.5f, 0.0f, 0.2f);
        Handles.DrawSolidArc(playerPos + Vector3.up * 0.1f, Vector3.up, leftBoundary, riposteAngle, _riposteRange);
        Handles.color = new Color(1.0f, 0.5f, 0.0f, 0.8f);
        Handles.DrawWireArc(playerPos + Vector3.up * 0.1f, Vector3.up, leftBoundary, riposteAngle, _riposteRange);
#endif

        // 락온 타깃 연결선
        if (CurrentLockOnTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(eyePos, CurrentLockOnTarget.position + Vector3.up * 1.2f);
            Gizmos.DrawWireSphere(CurrentLockOnTarget.position + Vector3.up * 1.2f, 0.35f);
        }
    }
    #endregion

    private void OnDestroy()
    {
        UnsubscribeInputEvents();
        _inputReader?.Dispose();

        if (_statusController != null)
        {
            _statusController.OnDieEvent -= HandleUnitDie;
        }
        EventBusSystem.Unsubscribe<GameStateChangedEvent>(HandleGameStateChanged);
    }
}