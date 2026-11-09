/// <summary>
/// Player 전용 FSM 상태 베이스 클래스
/// </summary>
public class PlayerBaseState : BaseState<PlayerController>
{
    protected readonly PlayerStateMachine _playerStateMachine;
    protected readonly PlayerStatusController _playerStatusController;
    protected readonly PlayerAnimController _playerAnimController; 
    public PlayerBaseState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine)
    {
        _playerStateMachine = stateMachine;
        _playerStatusController = owner.StatusController;
        _playerAnimController = owner.AnimationController;
    }
}