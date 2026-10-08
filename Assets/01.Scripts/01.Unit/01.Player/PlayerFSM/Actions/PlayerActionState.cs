/// <summary>
/// 공격, 구르기, 가드(패리)와 같이 플레이 기본 조작권 (상태 전이)를 
/// 부분적 또는 전면적으로 제한하고 캔슬 윈도우를 통제하는 Player FSM 추상 베이스 상태 클래스
/// </summary>
public class PlayerActionState : UnitBaseState<PlayerController>
{
    protected readonly PlayerStateMachine playerStateMachine;
    protected readonly PlayerStatController playerStatController;

    public bool CanMove { get; protected set; } = false;
    public bool CanRotate { get; protected set; } = false;
    public bool CanCancel { get; protected set; } = false;


    public PlayerActionState(PlayerController owner, PlayerStateMachine stateMachine) 
        : base(owner, stateMachine)
    {
        this.playerStateMachine = stateMachine;
        this.playerStatController = owner.PlayerStatController;
    }

    public override void Enter()
    {
        base.Enter();

        if(!CanMove)
        {
            owner.Movement.StopMovement();
        }
    }

    public override void Exit()
    {
        base.Exit();
        CanMove = false;
        CanRotate = false;
        CanCancel = false;
    }
}
