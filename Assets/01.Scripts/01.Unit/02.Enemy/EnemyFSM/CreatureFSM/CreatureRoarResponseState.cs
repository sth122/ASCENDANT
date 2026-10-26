/// <summary>
/// 크리처 전용 응답 포효 상태
/// 동료의 Roar 신호를 수신했을 때 호출 신호를 재전파하지 않고,
/// 단발성 포효 애니메이션만 완료한 뒤 즉각 추격(Chase) 상태로 전환합니다.
/// </summary>
public class CreatureRoarResponseState : EnemyBaseState
{
    private const float ResponseDuration = 1.6f;

    public CreatureRoarResponseState(CreatureEnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        owner.StopMovementAPI();

        // 재전파 브로드캐스트(BroadcastRoarToAllies)는 절대로 호출하지 않음
        _enemyAnimController.PlayState(AnimStateType.Roar);

        System.Threading.CancellationToken token = CreateStateToken();
        WaitForResponseFinishedAsync(token).Forget();
    }

    public override void Update()
    {
        base.Update();
        if (owner.CurrentTarget != null)
        {
            owner.FaceTargetAPI(owner.CurrentTarget.position, 8.0f, UnityEngine.Time.deltaTime);
        }
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid WaitForResponseFinishedAsync(System.Threading.CancellationToken token)
    {
        bool isCanceled = await Cysharp.Threading.Tasks.UniTask.Delay((int)(ResponseDuration * 1000f), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // 응답 모션 완료 후 즉시 추격 전이
        _enemyStateMachine.ChangeState((int)UnitState.Chase);
    }
}