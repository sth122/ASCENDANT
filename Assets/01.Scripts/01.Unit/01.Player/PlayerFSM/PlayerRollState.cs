using Cysharp.Threading.Tasks;

/// <summary>
/// Player 회피/구르기 동작을 전담하는 Player FSM 상태 클래스
/// 진입 시 이동 입력 여부에 따라 구르기 방향 또는 백스탭을 실행
/// 스태미나 소모 및 RollForce 임펄스를 적용하고, UniTask 비동기 루틴을 통해 무적 프레임 및 후딜레이 종료 타이밍 처리
/// 동작 중 모든 조작을 금지, 종료 시점에 선입력 버퍼에 따라 상태 전환
/// </summary>
public class PlayerRollState : PlayerActionState
{
    private System.Threading.CancellationTokenSource _rollCts;
    private const float RollStaminaCost = 18.0f;
    private const float InvincibleDuration = 0.35f;    // 무적 시간 (초 단위)
    private const float RollTotalDuration = 0.65f;     // 구르기 전체 동작 시간 (초 단위)

    public bool IsInvincible { get; private set; }

    public PlayerRollState(PlayerController owner, PlayerStateMachine statMachine)
        : base(owner, statMachine)  {  }

    public override void Enter()
    {
        base.Enter();

        // 1. 스태미나 차감
        _playerStatController?.ConsumeStamina(RollStaminaCost);

        // 2. 구르기 방향 결정 ( 입력 방향 우선, 없으면 백스탭 )
        UnityEngine.Vector3 rollDir = owner.CurrentMoveDirection.sqrMagnitude > 0.001f ?
            owner.CurrentMoveDirection : -owner.transform.forward;

        // 3. RollForce 물리 임펄스 가산
        float rollForce = _playerStatController != null ?
            _playerStatController.MotionStats.RollForce.Value : owner.StatController.MotionStats.RollForce.Value;

        owner.Movement.ApplyRoll(rollDir, rollForce);

        // 4. 비동기 무적 및 후딜레이 종료 루틴 가동 ( 다른 입력 전이 차단 )
        _rollCts = new System.Threading.CancellationTokenSource();
        HandleRollRoutineAsync(_rollCts.Token).Forget();
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

        // 1. 무적 시간 대기
        bool canceled = await UniTask.Delay((int)(InvincibleDuration * 1000f), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        IsInvincible = true;

        // 2. 잔여 후딜레이 대기
        float remainDelay = RollTotalDuration - InvincibleDuration;
        canceled = await UniTask.Delay((int)(remainDelay * 1000f), cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        // 3. 구르기 종료 직전, 선입력 버퍼 확인
        if (owner.ConsumeCommand(InputCommandType.Roll))
        {
            _playerStateMachine.ChangeState(UnitState.Roll);
            return;
        }

        if (owner.ConsumeCommand(InputCommandType.Attack))
        {
            _playerStateMachine.ChangeState(UnitState.Attack);
            return;
        }

        // 4. 선입력 액션이 없다면, 현재 실시간 이동 입력 여부에 따라 복귀
        if (owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
        {
            if (owner.InputReader.IsSprinting)
                _playerStateMachine.ChangeState(UnitState.Sprint);
            else
                _playerStateMachine.ChangeState(UnitState.Move);
        }
        else
        {
            _playerStateMachine.ChangeState(UnitState.Idle);
        }

    }
}
