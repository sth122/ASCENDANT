using Util = DebugLogger<PlayerController>;

public class PlayerController : UnitController<PlayerController>
{
    protected override void Awake()
    {
        base.Awake();
        _stateMachine = new PlayerStateMachine();
        
    }

    protected override void InitializeStateMachine()
    {
        if(_stateMachine != null && _stateMachine is PlayerStateMachine playerSM)
        {
            playerSM.SetUpPlayerState(this);
            playerSM.Initialize(UnitState.Idle);
        }
    }
}
