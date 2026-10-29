/// <summary>
/// 공격, 구르기, 가드(패리)와 같이 플레이 기본 조작권 (상태 전이)를 
/// 부분적 또는 전면적으로 제한하고 캔슬 윈도우를 통제하는 Player FSM 추상 베이스 상태 클래스
/// </summary>
public class PlayerActionState : PlayerBaseState
{
    public bool CanMove { get; protected set; } = false;
    public bool CanRotate { get; protected set; } = false;

    private System.Threading.CancellationTokenSource _actionCts;

    public PlayerActionState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        if (!CanMove)
        {
            owner.StopMovementAPI();
        }

        ResetActionToken();
    }

    public override void Exit()
    {
        base.Exit();
        CanMove = false;
        CanRotate = false;
        ResetActionToken();
    }

    protected System.Threading.CancellationToken CreateActionToken()
    {
        ResetActionToken();
        _actionCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(owner.destroyCancellationToken);
        return _actionCts.Token;
    }

    protected void ResetActionToken()
    {
        AsyncUtil.CancelAndDispose(ref _actionCts);
    }

    /// <summary>
    /// 액션 모션 종료 시 선입력 버퍼 큐를 검사하여 다음 상태로 안전하게 전이
    /// </summary>
    protected void CompleteActionAndEvaluateTransition()
    {
        // 1. 우선순위 버퍼 소비 (Roll > Parry > Attack > Jump)
        if (owner.InputReader.TryConsumePriorityCommand(out InputCommandType nextCommand))
        {
            switch (nextCommand)
            {
                case InputCommandType.Roll:
                    if (_playerStatusController != null && _playerStatusController.HasEnoughStamina(PlayerRollState.RollStaminaCost))
                    {
                        _playerStateMachine.ChangeState(UnitState.Roll, true);
                        return;
                    }
                    owner.InputReader.ClearCommand(InputCommandType.Roll);
                    break;

                case InputCommandType.Attack:
                    if (owner.TryTriggerRiposte()) return;

                    if (_playerStatusController != null && _playerStatusController.HasEnoughStamina(PlayerAttackState.AttackStaminaCost))
                    {
                        _playerStateMachine.ChangeState(UnitState.Attack, true);
                        return;
                    }
                    owner.InputReader.ClearCommand(InputCommandType.Attack);
                    break;

                case InputCommandType.Parry:
                    _playerStateMachine.ChangeState(UnitState.Parry, true);
                    return;

                case InputCommandType.Jump:
                    if (owner.Movement.IsGrounded && _playerStatusController != null && _playerStatusController.HasEnoughStamina(PlayerJumpState.JumpStaminaCost))
                    {
                        _playerStateMachine.ChangeState(UnitState.Jump);
                        return;
                    }
                    owner.InputReader.ClearCommand(InputCommandType.Jump);
                    break;
            }
        }

        // 2. 유효 선입력 액션이 없거나 스태미나가 부족할 경우 현재 이동 입력 상태로 복귀
        if (owner.InputReader.MoveInput.sqrMagnitude > 0.01f)
        {
            _playerStateMachine.ChangeState(owner.InputReader.IsSprinting ? UnitState.Sprint : UnitState.Move);
        }
        else
        {
            _playerStateMachine.ChangeState(UnitState.Idle);
        }
    }
}