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
    [Header("Combat Configurations")]
    [SerializeField] private PlayerCombatConfigSO _combatConfig;

    [Header("Lock-On Setting")]
    [SerializeField] private float _lockOnRadius = 15f;
    [SerializeField] private float _riposteRange = 2.2f;
    [SerializeField] private LayerMask _lockOnObstacleLayer;

    [Header("Camera Anchor")]
    [SerializeField] private Transform _cameraTarget;

    [Header("Combat Hitbox Component")]
    [SerializeField] private WeaponSweepHitbox _playerWeaponHitbox;

    private readonly Collider[] _lockOnHitBuffer = new Collider[16];
    private IRiposteable _currentRiposteTarget;

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
    public PlayerCombatConfigSO CombatConfig => _combatConfig;

    public Transform CameraTarget => _cameraTarget != null ? _cameraTarget : transform;
    public Transform CurrentLockOnTarget { get; private set; }
    public Vector3 CurrentMoveDirection { get; private set; }
    public IRiposteable CurrentRiposteTarget => _currentRiposteTarget;
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

        if (_playerWeaponHitbox == null)
        {
            _playerWeaponHitbox = GetComponentInChildren<WeaponSweepHitbox>();
        }
        _combatModule = new PlayerCombatModule(this, _playerWeaponHitbox);

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
        if (_stateMachine.CurrentState is PlayerKnockbackState knockdownState && knockdownState.IsInvincible) return;

        // 4. 처형 액션 중 무적
        if (_stateMachine.CurrentState is PlayerRiposteState) return;

        // 5. 가드 및 패링 판정 분기
        if (_stateMachine.CurrentState is PlayerGuardState guardState)
        {
            BlockResultType blockResult = guardState.ProcessIncomingAttack(in context);
            if (blockResult != BlockResultType.Failed) return;
        }

        // 6. 장판 도트 대미지 (DoT)
        if (context.ImpactType == AttackImpactType.DoT)
        {
            _statusController.TakeDamage(context.Damage, 0f);
            return;
        }

        // 단일 물리 방어력(DEF) 비선형 관통 연산
        float flatDef = _statusController.PlayerInfoSource != null ? _statusController.PlayerInfoSource.FlatDefense : 10.0f;
        flatDef += _statusController.EquippedArmorFlatDefense;

        float penetratedDmg = CombatFormula.CalculatePenetratedDamage(context.Damage, flatDef);

        // 방어구 4부위 합산 물리 경감률 적용
        float absorption = _statusController.EquippedArmorAbsorption;
        float finalDamage = penetratedDmg * (1.0f - absorption);

        // 7. 기본 대미지 및 포이즈 차감
        _statusController.TakeDamage(finalDamage, context.PoiseDamage);
        if (_statusController.CurrentHp <= 0f) return;

        // 8. 강공격 넉백 다운
        if (context.ImpactType == AttackImpactType.Heavy)
        {
            ApplyKnockbackImpulse(context.HitNormal, context.KnockbackForce);
            _stateMachine.ChangeState(UnitState.Knockback, true);
            return;
        }

        // 9. 공격 중 슈퍼아머 버티기 판정
        if (_stateMachine.CurrentState is PlayerAttackState)
        {
            if (!_statusController.IsPoiseBroken && !_statusController.IsPoiseDepleted) return;
        }

        // 10. 비공격 상태 일반 피격 경직
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
                    _currentRiposteTarget = riposteTarget;
                    _stateMachine.ChangeState(UnitState.Riposte);

                    System.Array.Clear(_lockOnHitBuffer, 0, hitCount);
                    return true;
                }
            }
        }

        System.Array.Clear(_lockOnHitBuffer, 0, hitCount);
        return false;
    }

    public void ClearRiposteTarget() => _currentRiposteTarget = null;
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