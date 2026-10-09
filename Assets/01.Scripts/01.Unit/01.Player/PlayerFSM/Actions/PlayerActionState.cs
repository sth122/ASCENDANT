/// <summary>
/// 공격, 구르기, 가드(패리)와 같이 플레이 기본 조작권 (상태 전이)를 
/// 부분적 또는 전면적으로 제한하고 캔슬 윈도우를 통제하는 Player FSM 추상 베이스 상태 클래스
/// </summary>
public class PlayerActionState : UnitBaseState<PlayerController>
{
    protected readonly PlayerStateMachine _playerStateMachine;
    protected readonly PlayerStatController _playerStatController;

    public bool CanMove { get; protected set; } = false;
    public bool CanRotate { get; protected set; } = false;

    public PlayerActionState(PlayerController owner, PlayerStateMachine stateMachine) 
        : base(owner, stateMachine)
    {
        this._playerStateMachine = stateMachine;
        this._playerStatController = owner.PlayerStatController;
    }

    public override void Enter()
    {
        base.Enter();

        if(!CanMove)
        {
            owner.StopMovementAPI();
        }
    }

    public override void Exit()
    {
        base.Exit();
        CanMove = false;
        CanRotate = false;
    }

    /// <summary>
    /// 액션 모션 종료 시 호출되는 상태 전이 처리기
    /// 버퍼에 대기 중인 다음 커맨드가 있으면 해당 액션 상태로 전이
    /// 없으면 현재 이동 입력 상태로 전이
    /// </summary>
    protected void CompleteActionAndEvaluateTransition()
    {
        // 1. 유효 선입력 커맨드 확인
        if(owner.InputReader.TryConsumeAnyCommand(out InputCommandType nextCommand))
        {
            switch(nextCommand)
            {
                case InputCommandType.Roll:
                    _playerStateMachine.ChangeState(UnitState.Roll, true);
                    return;
                case InputCommandType.Attack:
                    _playerStateMachine.ChangeState(UnitState.Attack, true);
                    return;
                case InputCommandType.Parry:
                    _playerStateMachine.ChangeState(UnitState.Parry, true);
                    return;
                case InputCommandType.Jump:
                    if (owner.Movement.IsGrounded)
                    {
                        _playerStateMachine.ChangeState(UnitState.Jump);
                        return;
                    }
                    break;
            }
        }

        // 2. 선입력 액션이 없으면 현재 지속 이동 상태로 전이
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
