using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 크리처 전용 표효 상태 (UnitState.Roar <-> CreatureRoarState)
/// </summary>
public class CreatureRoarState : EnemyBaseState
{
    private const float RoarDuration = 2.0f;
    private bool _isRoarCompleted = false;

    public CreatureRoarState(CreatureEnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        _isRoarCompleted = false;
        owner.StopMovementAPI();

        _enemyAnimController.PlayState(AnimStateType.Roar);

        if (owner is CreatureEnemyController creature)
        {
            creature.BroadcastRoarToAllies();
        }

        System.Threading.CancellationToken token = CreateStateToken();
        WaitForRoarFinishedAsync(token).Forget();
    }

    public override void Update()
    {
        base.Update();

        // 포효 모션 초기 선딜레이 동안 타깃을 매끄럽게 정면 주시
        if (!_isRoarCompleted && owner.CurrentTarget != null)
        {
            owner.FaceTargetAPI(owner.CurrentTarget.position, 10.0f, Time.deltaTime);
        }
    }

    private async UniTaskVoid WaitForRoarFinishedAsync(System.Threading.CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay(2000, cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        _isRoarCompleted = true;
        _enemyStateMachine.ChangeState((int)UnitState.Chase);
    }
}