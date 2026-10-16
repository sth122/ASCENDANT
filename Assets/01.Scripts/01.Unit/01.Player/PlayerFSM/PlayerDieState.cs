public class PlayerDieState : PlayerBaseState
{
    public PlayerDieState(PlayerController owner, PlayerStateMachine stateMachine) : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        owner.StopMovementAPI();
        owner.Rigidbody.isKinematic = true;

        owner.InputReader.DisablePlayerInput();

        // Any State -> death 트리거 발동
        _animController.SetTrigger(AnimParamType.Die);
    }
}
