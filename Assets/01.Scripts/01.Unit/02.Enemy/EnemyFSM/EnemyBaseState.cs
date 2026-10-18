public class EnemyBaseState : UnitBaseState<EnemyController>
{
    protected readonly EnemyStateMachine _enemyStateMachine;
    protected readonly EnemyStatController _enemyStatController;
    protected readonly EnemyAnimController _enemyAnimController;
    protected System.Threading.CancellationTokenSource _stateCts;

    public EnemyBaseState(EnemyController owner, EnemyStateMachine stateMachine) : base(owner, stateMachine)
    {
        _enemyStateMachine = stateMachine;
        _enemyStatController = owner.EnemyStatController;
        _enemyAnimController = owner.EnemyAnimController;
    }

    public override void Exit()
    {
        base.Exit();
        AsyncUtil.CancelAndDispose(ref _stateCts);
    }

    public System.Threading.CancellationToken CreateStateToken()
    {
        AsyncUtil.CancelAndDispose(ref _stateCts);
        _stateCts = new System.Threading.CancellationTokenSource();
        return _stateCts.Token;
    }
}
