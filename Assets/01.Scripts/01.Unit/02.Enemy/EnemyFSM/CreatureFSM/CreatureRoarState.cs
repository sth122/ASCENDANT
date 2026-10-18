using Cysharp.Threading.Tasks;

/// <summary>
/// 크리처 전용 표효 상태 (UnitState.Roar <-> CreatureRoarState)
/// </summary>
public class CreatureRoarState : EnemyBaseState
{
    public CreatureRoarState(CreatureEnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        owner.StopMovementAPI();

        if (owner.CurrentTarget != null)
        {
            owner.FaceTargetAPI(owner.CurrentTarget.position, 15.0f, UnityEngine.Time.deltaTime);
        }

        _enemyAnimController.PlayState(AnimStateType.Roar);

        if (owner is CreatureEnemyController creature)
        {
            creature.BroadcastRoarToAllies();
        }

        System.Threading.CancellationToken token = CreateStateToken();
        WaitForRoarFinishedAsync(token).Forget();
    }

    private async UniTaskVoid WaitForRoarFinishedAsync(System.Threading.CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay(2000, delayTiming: PlayerLoopTiming.Update, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        _enemyStateMachine.ChangeState((int)UnitState.Chase);
    }
}