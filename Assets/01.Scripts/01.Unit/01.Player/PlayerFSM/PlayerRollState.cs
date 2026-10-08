using Cysharp.Threading.Tasks;

/// <summary>
/// Player 회피/구르기 동작을 전담하는 Player FSM 상태 클래스
/// 진입 시 스태미나 소모 및 RollForce 임펄스를 적용하고, UniTask 비동기 루틴을 통해 무적 프레임 및 후딜레이 종료 타이밍 처리
/// </summary>
public class PlayerRollState : UnitBaseState<PlayerController>
{
    private readonly PlayerStateMachine _playerStateMachine;
    private readonly PlayerStatController _playerStatController;
    private System.Threading.CancellationTokenSource _rollCts;

    private const float RollStaminaCost = 5f;
    private const float InvincibleDuration = 1f;    // 무적 시간 (초 단위)
    private const float RollTotalDuration = 1f;     // 구르기 전체 동작 시간 (초 단위)

    public bool IsInvincible { get; private set; }

    public PlayerRollState(PlayerController owner, PlayerStateMachine statMachine)
        : base(owner, statMachine)
    {
        _playerStateMachine = statMachine;
        _playerStatController = owner.PlayerStatController;
    }

    public override void Enter()
    {
        base.Enter();

        // 1. 스태미나 차감
        _playerStatController?.ConsumeStamina(RollStaminaCost);

        // 2. 구르기 방향 결정 ( 입력 방향 우선, 없으면 정면 )
        UnityEngine.Vector3 rollDir = owner.CurrentMoveDirection.sqrMagnitude > 0.001f
            ? owner.CurrentMoveDirection : owner.transform.forward;

        // 3. RollForce 물리 임펄스 가산
        float rollForce = _playerStatController != null ?
            _playerStatController.MotionStats.RollForce.Value : owner.StatController.MotionStats.RollForce.Value;

        owner.Movement.ApplyRoll(rollDir, rollForce);

        _rollCts = new System.Threading.CancellationTokenSource();
        HandleRollRoutineAsync(_rollCts.Token).Forget();
    }
    public override void Update()
    {
        base.Update();
        // 구르기 진행 중에는 다른 입력 전이를 잠금
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();
    }

    public override void Exit()
    {
        base.Exit();

        IsInvincible = false;

        if (_rollCts != null)
        {
            _rollCts.Cancel();
            _rollCts.Dispose();
            _rollCts = null;
        }
    }

    private async UniTaskVoid HandleRollRoutineAsync(System.Threading.CancellationToken token)
    {
        IsInvincible = true;

        bool canceled = await UniTask.Delay((int)(InvincibleDuration * 1000f), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        float remainDelay = RollTotalDuration - InvincibleDuration;
        canceled = await UniTask.Delay((int)(remainDelay * 1000f), cancellationToken: token).SuppressCancellationThrow();
        if(canceled) return;

        if(owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
        {
            if(owner.InputReader.IsSprinting)
            {
                _playerStateMachine.ChangeState(UnitState.Sprint);
            }
            else
            {
                _playerStateMachine.ChangeState(UnitState.Move);
            }
        }
        else
        {
            _playerStateMachine.ChangeState(UnitState.Idle);
        }
    
    }
}
