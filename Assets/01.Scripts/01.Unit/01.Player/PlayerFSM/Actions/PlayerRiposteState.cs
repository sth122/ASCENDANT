using Cysharp.Threading.Tasks;
/// <summary>
/// Player 그로기 상태의 적 앞잡(Riposte / 처형) 시네마틱 액션 상태 클래스
/// 무적 상태로 시네마틱 공격을 가하고 대형 치명타 데미지를 주입
/// </summary>
public class PlayerRiposteState : PlayerActionState
{
    private const float RiposteDuration = 1.8f;
    private UnityEngine.Transform _targetEnemy;
    private UnitStatusController _targetEnemyStat;

    public PlayerRiposteState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public void SetRiposteTarget(UnityEngine.Transform targetEnemy, UnitStatusController enemyStat)
    {
        _targetEnemy = targetEnemy;
        _targetEnemyStat = enemyStat;
    }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;
        owner.StopMovementAPI();

        // 1. 처형 대상 적을 정면으로 정렬
        if (_targetEnemy != null)
        {
            // 적의 전방 일정 거리로 위치 보정
            UnityEngine.Vector3 snapPos = _targetEnemy.position + _targetEnemy.forward * 1.2f;
            snapPos.y = owner.transform.position.y;
            owner.transform.position = snapPos;

            UnityEngine.Vector3 lookDirection = _targetEnemy.position - owner.transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                owner.transform.rotation = UnityEngine.Quaternion.LookRotation(lookDirection);
            }
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
        _targetEnemy = null;
        _targetEnemyStat = null;
        _playerAnimController.ResetTrigger(AnimParamType.Riposte);
    }
    private async UniTaskVoid ExecuteRiposteAsync(System.Threading.CancellationToken token)
    {
        // 모션 절반 시점에 치명타 대미지 주입
        bool canceledHalf = await UniTask.Delay((int)((RiposteDuration * 0.45f) * 1000f),
            cancellationToken: token).SuppressCancellationThrow();
        if (canceledHalf) return;

        if (_targetEnemy != null && _targetEnemy.TryGetComponent(out IDamageableTest damageable))
        {
            owner.CombatModule.ExecuteRiposteDamage(damageable, _targetEnemy.position);
        }

        // 전체 모션 완료 대기
        bool canceledFinish = await UniTask.Delay((int)((RiposteDuration * 0.55f) * 1000f),
            cancellationToken: token).SuppressCancellationThrow();
        if (canceledFinish) return;

        if(_targetEnemyStat != null)
        {
            _targetEnemyStat.RestorePoiseToMax();
        }

        CompleteActionAndEvaluateTransition();
    }
}
