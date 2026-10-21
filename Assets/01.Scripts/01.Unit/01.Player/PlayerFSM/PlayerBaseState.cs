public class PlayerBaseState : UnitBaseState<PlayerController>
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
