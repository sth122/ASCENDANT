using Cysharp.Threading.Tasks;

public class EnemyAttackState : EnemyBaseState
{
    public EnemyAttackState(EnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        owner.StopMovementAPI();

        EnemyAttackData pattern = owner.Combat.CurrentSelectedPattern;
        UnityEngine.Transform target = owner.CurrentTarget;

        if (target == null)
        {
            _enemyStateMachine.ChangeState((int)UnitState.Chase);
            return;
        }

        owner.Combat.StartAttack(target, in pattern);
        _enemyAnimController.OnActionCompletedEvent += HandleAttackCompleted;

        System.Threading.CancellationToken token = CreateStateToken();
        WaitForAttackTimeoutAsync(token).Forget();
    }

    private void HandleAttackCompleted()
    {
        _enemyStateMachine.ChangeState((int)UnitState.Chase);
    }

    private async UniTaskVoid WaitForAttackTimeoutAsync(System.Threading.CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay(2000, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        HandleAttackCompleted();
    }

    public override void Exit()
    {
        _enemyAnimController.OnActionCompletedEvent -= HandleAttackCompleted;
        owner.Combat.StopAttack();
        base.Exit();
    }
}
