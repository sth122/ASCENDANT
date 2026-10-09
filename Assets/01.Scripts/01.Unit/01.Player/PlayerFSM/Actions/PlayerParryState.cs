using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Player 방패 가드 유지 및 패링을 전담하는 Player FSM 액션 상태 클래스
/// 가드를 올린 직후 짧은 프레임 동안 패링 판정을 활성화하고,
/// 가드 유지 중에는 이동 속도 및 스태미나 회복 속도 감소
/// </summary>
public class PlayerParryState : PlayerActionState
{
    private System.Threading.CancellationTokenSource _parryCts;
    #region MyRegion
    private const float ParryWindowDuration = 0.2f;             // 패링 유효 시간
    private const float GuardMoveSpeedFactor = 0.75f;            // 가드 중 걷기 속도 배율
    private const float GuardStaminaRegenMulitplier = 0.25f;    // 가드 중 스태미나 회복 배율
    private float ShieldStability = 0.65f;                      // 기본 방패 버티기 감쇄력
    #endregion

    public bool IsParryWindow { get; private set; }

    public PlayerParryState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }


    public override void Enter()
    {
        base.Enter();

        // 1. 가드 상태는 저속 이동과 방향 전환 가능
        CanMove = true;
        CanRotate = true;

        // 2. 스태미나 회복 속도 저하
        if (_playerStatController != null)
        {
            _playerStatController.StaminaRegenMultiplier = GuardStaminaRegenMulitplier;
        }

        EnableEventHandler();

        // 3. 패링 판정 타이머
        _parryCts = new System.Threading.CancellationTokenSource();
        ActivateParryWindowAsync(_parryCts.Token).Forget();
    }

    public override void Update()
    {
        base.Update();

        // 1. 회피(Roll) 선입력 캔슬 우선 체크 (위급 시 가드 풀고 굴러서 회피)
        if (owner.InputReader.TryConsumeCommand(InputCommandType.Roll))
        {
            _playerStateMachine.ChangeState(UnitState.Roll);
            return;
        }

        // 2. 가드 키(마우스 우클릭 등)를 뗐다면 즉시 상태 해제
        if (!owner.InputReader.IsGuarding)
        {
            ReturnToGroundState();
            return;
        }
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // 가드 중 저속 보행 이동
        if (owner.CurrentMoveDirection.sqrMagnitude > 0.001f)
        {
            float guardSpeed = owner.StatController.MotionStats.WalkSpeed.Value * GuardMoveSpeedFactor;
            owner.MoveAPI(owner.CurrentMoveDirection, guardSpeed);

            // 락온이 아닐 때 이동 방향으로 조향
            if (owner.CurrentLockOnTarget == null)
            {
                owner.RotateTowardsAPI(owner.CurrentMoveDirection, owner.GetPlayerStat().RotationSpeed, Time.fixedDeltaTime);
            }
        }
        else
        {
            owner.StopMovementAPI();
        }
    }

    public override void Exit()
    {
        base.Exit();

        DisableEventHandler();

        IsParryWindow = false;

        if (_playerStatController != null)
        {
            _playerStatController.StaminaRegenMultiplier = 1.0f;
        }

        if (_parryCts != null)
        {
            _parryCts.Cancel();
            _parryCts.Dispose();
            _parryCts = null;
        }
    }

    public void OnHitBlocked(float damage, GameObject attacker)
    {
        // 1. ParryWindow 타이밍에 적중했을 시 패링 카운터 발동
        if (IsParryWindow)
        {
            TriggerParrySuccess(attacker);
            return;
        }

        // 2. 일반 가드 피격 시 스태미나 차감
        if (_playerStatController != null)
        {
            bool isGuardBroken = _playerStatController.BlockAttackWithStamina(damage, ShieldStability);

            if (isGuardBroken)
            {
                _playerStateMachine.ChangeState(UnitState.Stun);
            }
            else
            {
                // 가드 히트 사운드 및 이펙트 처리 추가
            }
        }
    }

    private void TriggerParrySuccess(GameObject attacker)
    {
        // 적 유닛 피격/경직 인터페이스 호출
        if (attacker.TryGetComponent(out UnitStatController enemyStat))
        {
            enemyStat.TakeDamage(0f, 999f);
        }

        // 패링 성공 연출
    }

    private async UniTaskVoid ActivateParryWindowAsync(System.Threading.CancellationToken token)
    {
        IsParryWindow = true;

        bool canceled = await UniTask.Delay((int)(ParryWindowDuration * 1000f), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        // 패링 타이밍 종료. 이후 유지 시 일반 가드로만 유지
        IsParryWindow = false;
    }

    private void ReturnToGroundState()
    {
        if (owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
        {
            _playerStateMachine.ChangeState(owner.InputReader.IsSprinting ? UnitState.Sprint : UnitState.Move);
        }
        else
        {
            _playerStateMachine.ChangeState(UnitState.Idle);
        }
    }

    #region EventHandler Func
    private void EnableEventHandler()
    {
        DisableEventHandler();
        owner.InputReader.OnCommandInputEvent += HandleCommandDuringGuard;
        owner.InputReader.OnGuardReleased += HandleGuardReleased;
    }
    private void DisableEventHandler()
    {
        owner.InputReader.OnCommandInputEvent -= HandleCommandDuringGuard;
        owner.InputReader.OnGuardReleased -= HandleGuardReleased;
    }
    private void HandleCommandDuringGuard(InputCommandType command)
    {
        // 가드 중 위급 상황 시 즉시 가드를 풀고 회피 전이
        if (command == InputCommandType.Roll)
        {
            _playerStateMachine.ChangeState(UnitState.Roll);
        }
    }

    private void HandleGuardReleased()
    {
        // 방패 버튼을 떼면 즉시 지상 기본 상태로 복귀
        ReturnToGroundState();
    }
    #endregion
}

