using UnityEngine;

public class PlayerBaseState : UnitBaseState<PlayerController>
{
    protected readonly PlayerStateMachine _playerStateMachine;
    protected readonly PlayerStatController _playerStatController;
    protected readonly PlayerAnimController _animController;

    public PlayerBaseState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine)
    {
        _playerStateMachine = stateMachine;
        _playerStatController = owner.PlayerStatController;
        _animController = owner.AnimationController;
    }
}
