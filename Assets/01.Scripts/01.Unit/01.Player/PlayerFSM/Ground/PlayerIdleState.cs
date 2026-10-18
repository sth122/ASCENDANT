/// <summary>
/// Player 정지 대기 상태를 전담하는 FSM 상태 클래스
/// </summary>
public class PlayerIdleState : PlayerGroundState
{
    public PlayerIdleState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        // 이전 상태 잔여 관성 정지
        owner.StopMovementAPI();

        _playerAnimController.SetFloat(AnimParamType.MoveX, 0f);
        _playerAnimController.SetFloat(AnimParamType.MoveZ, 0f);
    }

    protected override void HandleMoveInputChanged(UnityEngine.Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude > 0.01f)
        {
            _playerStateMachine.ChangeState(UnitState.Move);
        }
    }
}
