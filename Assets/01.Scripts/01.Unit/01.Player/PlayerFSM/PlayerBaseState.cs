/// <summary>
/// Player 전용 FSM 상태 베이스 클래스
/// </summary>
public class PlayerBaseState : BaseState<PlayerController>
{
    protected readonly PlayerStateMachine _playerStateMachine;
    protected readonly PlayerStatusController _playerStatusController;
    protected readonly PlayerAnimController _playerAnimController;
#if UNITY_EDITOR
    private readonly string _stateTypeName;
#endif
    public PlayerBaseState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine)
    {
        _playerStateMachine = stateMachine;
        _playerStatusController = owner.StatusController;
        _playerAnimController = owner.AnimationController;
#if UNITY_EDITOR
        _stateTypeName = GetType().Name;
#endif
    }

    public override void Enter()
    {
#if UNITY_EDITOR
        DebugLogger<PlayerController>.Log($"[{_stateTypeName}] 진입");
#endif
    }
    public override void Exit()
    {
#if UNITY_EDITOR
        DebugLogger<PlayerController>.Log($"[{_stateTypeName}] 퇴장");
#endif
    }
}