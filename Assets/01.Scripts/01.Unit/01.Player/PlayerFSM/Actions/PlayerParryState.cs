using Cysharp.Threading.Tasks;

using UnityEngine;

/// <summary>
/// Player 방패 가드 유지 및 패링을 전담하는 Player FSM 액션 상태 클래스
/// 가드를 올린 직후 짧은 프레임 동안 패링 판정을 활성화하고,
/// 가드 유지 중에는 이동 속도 및 스태미나 회복 속도 감소
/// </summary>
public class PlayerParryState : PlayerActionState
{
    private GameObject _targetAttacker;

    public PlayerParryState(PlayerController owner, PlayerStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        CanMove = false;
        CanRotate = false;
        owner.StopMovementAPI();

        // 주입받은 공격자에게 패링 피격 즉시 통보
        if (_targetAttacker != null)
        {
            EnemyBTController enemy = _targetAttacker.GetComponentInParent<EnemyBTController>();
            if (enemy != null)
            {
                enemy.OnParried();
            }
            _targetAttacker = null;
        }

        // 2. 플레이어 본체 패링 성공 반격 모션 격발
        _playerAnimController.SetTrigger(AnimParamType.ParrySuccess);

        // 3. 반격 모션 완료 대기 시퀀스 가동
        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteParrySuccessSequenceAsync(token).Forget();
    }

    private async UniTaskVoid ExecuteParrySuccessSequenceAsync(System.Threading.CancellationToken token)
    {
        float duration = _playerAnimController.GetStateDuration(AnimStateType.Parry);

        bool canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(duration),
            delayTiming: PlayerLoopTiming.Update,
            cancellationToken: token).SuppressCancellationThrow();

        if (canceled) return;

        // 반격 모션 종료 시 C키(가드)를 계속 누르고 있다면 다시 PlayerGuardState로 복귀
        if (owner.InputReader.IsGuarding)
        {
            _playerStateMachine.ChangeState(UnitState.Guard);
        }
        else
        {
            CompleteActionAndEvaluateTransition();
        }
    }

    public void SetParriedAttacker(GameObject attacker) => _targetAttacker = attacker;
    public override void Exit()
    {
        base.Exit();
        _targetAttacker = null;
        _playerAnimController.ResetTrigger(AnimParamType.ParrySuccess);
    }
}