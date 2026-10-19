/// <summary>
/// 공격, 구르기, 가드(패리)와 같이 플레이 기본 조작권 (상태 전이)를 
/// 부분적 또는 전면적으로 제한하고 캔슬 윈도우를 통제하는 Player FSM 추상 베이스 상태 클래스
/// </summary>
public class PlayerActionState : PlayerBaseState
{
    public bool CanMove { get; protected set; } = false;
    public bool CanRotate { get; protected set; } = false;

    // 공통 비동기 토큰 소스
    private System.Threading.CancellationTokenSource _actionCts;

    public PlayerActionState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        if(!CanMove)
        {
            owner.StopMovementAPI();
        }

        // 상태 진입 시 이전 작업 토큰 정리
        ResetActionToken();
    }

    public override void Exit()
    {
        base.Exit();
        CanMove = false;
        CanRotate = false;

        // 상태 종료 시 가동 중인 비동기 루틴 즉시 취소 및 토큰 해제
        ResetActionToken();
    }

    /// <summary>
    /// 파생 액션 상태에서 비동기 타이머 시작 시 안전한 연동 토큰 생성
    /// </summary>
    protected System.Threading.CancellationToken CreateActionToken()
    {
        ResetActionToken();
        _actionCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(owner.destroyCancellationToken);
        return _actionCts.Token;
    }

    /// <summary>
    /// 토큰 취소 및 메모리 해제
    /// </summary>
    protected void ResetActionToken()
    {
        AsyncUtil.CancelAndDispose(ref _actionCts);
    }

    /// <summary>
    /// 액션 모션 종료 시 호출되는 상태 전이 처리기
    /// 버퍼에 대기 중인 다음 커맨드가 있으면 해당 액션 상태로 전이
    /// 없으면 현재 이동 입력 상태로 전이
    /// </summary>
    protected void CompleteActionAndEvaluateTransition()
    {
        // 1. 유효 우선 순위 버퍼 소비(Roll > Parry > Attack > Jump)
        if(owner.InputReader.TryConsumePriorityCommand(out InputCommandType nextCommand))
        {
            switch(nextCommand)
            {
                case InputCommandType.Roll:
                    _playerStateMachine.ChangeState(UnitState.Roll, true);
                    return;
                case InputCommandType.Attack:
                    // 앞잡 가능 대상이 있으면 앞잡 우선 트리거
                    if (owner.TryTriggerRiposte()) return;

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

        // 2. 유효 선입력 액션이 없으면 현재 지속 이동 상태로 전이
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
