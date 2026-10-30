using Cysharp.Threading.Tasks;
/// <summary>
/// Player 그로기 상태의 적 앞잡(Riposte / 처형) 시네마틱 액션 상태 클래스
/// 무적 상태로 시네마틱 공격을 가하고 대형 치명타 데미지를 주입
/// </summary>
public class PlayerRiposteState : PlayerActionState
{
    private const float RiposteDuration = 1.8f;
    private IRiposteable _riposteTarget;

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
        owner.StopMovementAPI();

        // 1. IRiposteable의 소켓 위치를 기준으로 플레이어 스냅 정렬
        if (_riposteTarget != null && _riposteTarget.RiposteSocket != null)
        {
            owner.transform.position = _riposteTarget.RiposteSocket.position;
            owner.transform.rotation = _riposteTarget.RiposteSocket.rotation;
        }

        // 2. 애니메이터 Riposte 트리거 작동
        _playerAnimController.SetTrigger(AnimParamType.Riposte);

        // 3. 비동기 처형 타이머 가동 (모션 중 무적)
        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteRiposteAsync(token).Forget();
    }

    public override void Exit()
    {
        base.Exit();
        _riposteTarget = null;
        _playerAnimController.ResetTrigger(AnimParamType.Riposte);
    }
    private async UniTaskVoid ExecuteRiposteAsync(System.Threading.CancellationToken token)
    {
        // 모션 절반 시점에 치명타 대미지 주입
        bool canceledHalf = await UniTask.Delay((int)((RiposteDuration * 0.45f) * 1000f),
            cancellationToken: token).SuppressCancellationThrow();
        if (canceledHalf) return;

        if (_riposteTarget != null && _riposteTarget is IDamageable damageable)
        {
            owner.CombatModule.ExecuteRiposteDamage(damageable, owner.transform.position);
            _riposteTarget.OnRiposted(owner.transform);
        }

        bool canceledFinish = await UniTask.Delay((int)((RiposteDuration * 0.55f) * 1000f),
            cancellationToken: token).SuppressCancellationThrow();
        if (canceledFinish) return;

        CompleteActionAndEvaluateTransition();
    }
}
