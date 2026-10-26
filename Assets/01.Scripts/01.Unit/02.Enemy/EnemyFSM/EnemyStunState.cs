/// <summary>
/// Enemy 그로기 스턴 상태
/// 플레이어의 앞잡 처형 진입 시 비동기 타이머를 즉시 정지
/// </summary>
public class EnemyStunState : EnemyBaseState
{
    private const float StunDuration = 3.5f;
    private bool _isLockedByRiposte = false;

    public EnemyStunState(EnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        _isLockedByRiposte = false;
        owner.StopMovementAPI();
        owner.CanBeRiposted = true; // 앞잡 허용 플래그 On

        // 그로기 연출을 위해 임시로  Hit2 모션 재생 - 강경직 모션을 만들던가 해야함
        _enemyAnimController.PlayState(AnimStateType.Hit2);

        System.Threading.CancellationToken token = CreateStateToken();
        WaitForStunRecoveryAsync(token).Forget();
    }

    public override void Exit()
    {
        base.Exit();
        owner.CanBeRiposted = false; // 앞잡 허용 플래그 회수
        _isLockedByRiposte = false;
        // 스턴 종료 시 강인도를 100% 완전 복구하여 다음 전투 준비
        owner.StatusController?.RestorePoiseToMax();
    }

    public void LockForRiposte()
    {
        _isLockedByRiposte = true;
        owner.CanBeRiposted = false;
        AsyncUtil.CancelAndDispose(ref _stateCts);
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid WaitForStunRecoveryAsync(System.Threading.CancellationToken token)
    {
        bool isCanceled = await Cysharp.Threading.Tasks.UniTask.Delay((int)(StunDuration * 1000f), 
            cancellationToken: token).SuppressCancellationThrow();

        if (isCanceled) return;

        // 스턴에서 깨어나면 타깃 생존 여부에 따라 Chase 또는 Idle로 복귀
        if (owner.CurrentTarget != null)
        {
            UnitStatusController targetStat = owner.CurrentTarget.GetComponentInParent<UnitStatusController>();
            if (targetStat != null && !targetStat.IsDead)
            {
                _enemyStateMachine.ChangeState((int)UnitState.Chase);
                return;
            }
        }

        _enemyStateMachine.ChangeState((int)UnitState.Idle);
    }
}
