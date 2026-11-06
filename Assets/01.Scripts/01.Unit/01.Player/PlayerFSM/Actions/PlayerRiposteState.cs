using Cysharp.Threading.Tasks;
/// <summary>
/// Player 그로기 상태의 적 앞잡(Riposte / 처형) 시네마틱 액션 상태 클래스
/// 무적 상태로 시네마틱 공격을 가하고 대형 치명타 데미지를 주입
/// </summary>
public class PlayerRiposteState : PlayerActionState
{
    private const float DefaultRiposteDuration = 2.0f;
    private IRiposteable _riposteTarget;
    public bool IsInvincible { get; private set; } = false;

    public PlayerRiposteState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public void SetRiposteTarget(IRiposteable target)
    {
        _riposteTarget = target;
    }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;
        IsInvincible = true;
        owner.StopMovementAPI();

        _riposteTarget = owner.CurrentRiposteTarget;

        // 1. 피격 대상에게 처형 개시 통보 (적 그로기 회복 타이머 즉각 동결)
        if (_riposteTarget != null && _riposteTarget.RiposteSocket != null)
        {
            _riposteTarget.OnRiposted(owner.transform);
        }

        // 2. 플레이어 처형 애니메이션 재생
        _playerAnimController.SetTrigger(AnimParamType.Riposte);

        // 3. 비동기 처형 타이머 가동 (모션 중 무적)
        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteRiposteAsync(token).Forget();
    }

    public override void Exit()
    {
        base.Exit();
        IsInvincible = false;
        _playerAnimController.ResetTrigger(AnimParamType.Riposte);

        // 4. 처형 종료 시 적에게 복귀 신호 전달
        if (_riposteTarget != null && !_riposteTarget.IsDead)
        {
            _riposteTarget.OnRiposteFinished();
        }
        _riposteTarget = null;
        owner.ClearRiposteTarget();
    }
    private async UniTaskVoid ExecuteRiposteAsync(System.Threading.CancellationToken token)
    {
        float halfTime = DefaultRiposteDuration * 0.45f;
        bool canceledHalf = await UniTask.Delay(System.TimeSpan.FromSeconds(halfTime), cancellationToken: token).SuppressCancellationThrow();
        if (canceledHalf) return;

        if (_riposteTarget != null && _riposteTarget is IDamageable damageable)
        {
            owner.CombatModule.ExecuteRiposteDamage(damageable, owner.transform.position);
        }

        float remainTime = DefaultRiposteDuration * 0.55f;
        bool canceledFinish = await UniTask.Delay(System.TimeSpan.FromSeconds(remainTime), cancellationToken: token).SuppressCancellationThrow();
        if (canceledFinish) return;

        CompleteActionAndEvaluateTransition();
    }
}
