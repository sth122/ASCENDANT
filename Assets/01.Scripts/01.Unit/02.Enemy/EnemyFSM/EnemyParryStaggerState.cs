/// <summary>
/// Enemy 패링 피격 전용 단기 경직(ParryStagger) FSM 상태 클래스
/// 공격이 쳐내졌을 때 0.8초간 짧게 굳으며, 이 상태에서는 앞잡(Riposte)이 발동되지 않음
/// </summary>
public class EnemyParryStaggerState : EnemyBaseState
{
    private const float StaggerDuration = 1.5f; // 짧은 피패링 경직 시간

    public EnemyParryStaggerState(EnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        owner.StopMovementAPI();
        owner.CanBeRiposted = false; // 단순 패링 경직 중에는 앞잡 절대 불가

        // 피패링 모션 재생 (Hit1 단발 경직)
        _enemyAnimController.PlayState(AnimStateType.Hit1);

        System.Threading.CancellationToken token = CreateStateToken();
        WaitForStaggerEndAsync(token).Forget();
    }

    public override void Exit()
    {
        base.Exit();
        owner.CanBeRiposted = false;
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid WaitForStaggerEndAsync(System.Threading.CancellationToken token)
    {
        bool isCanceled = await Cysharp.Threading.Tasks.UniTask.Delay((int)(StaggerDuration * 1000f),
            cancellationToken: token).SuppressCancellationThrow();

        if (isCanceled) return;

        // 경직 회복 후 플레이어 추적 복귀
        if (owner.CurrentTarget != null)
        {
            _enemyStateMachine.ChangeState((int)UnitState.Chase);
        }
        else
        {
            _enemyStateMachine.ChangeState((int)UnitState.Idle);
        }
    }
}