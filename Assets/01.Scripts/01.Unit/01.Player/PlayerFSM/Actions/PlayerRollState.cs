/// <summary>
/// Player 회피/구르기 동작을 전담하는 Player FSM 상태 클래스
/// 진입 시 이동 입력 여부에 따라 구르기 방향 또는 백스탭을 실행
/// 스태미나 소모 및 RollForce 임펄스를 적용하고, UniTask 비동기 루틴을 통해 무적 프레임 및 후딜레이 종료 타이밍 처리
/// 동작 중 모든 조작을 금지, 종료 시점에 선입력 버퍼에 따라 상태 전환
/// </summary>
public class PlayerRollState : PlayerActionState
{
    private const float RollStaminaCost = 18.0f;
    private const float InvincibleDuration = 0.35f;    // 무적 시간 (초 단위)
    private const float RollTotalDuration = 0.8f;     // 구르기 전체 동작 시간 (초 단위)

    public bool IsInvincible { get; private set; }

    public PlayerRollState(PlayerController owner, PlayerStateMachine statMachine)
        : base(owner, statMachine)  {  }

    public override void Enter()
    {
        base.Enter();

        // 1. 구르기 방향 결정 (입력 방향 우선, 무입력 시 백스탭 대용 180도 반전 - 현재 잠시 해제)
        UnityEngine.Vector3 moveInputDirection = owner.CurrentMoveDirection;
        bool isBackstep = moveInputDirection.sqrMagnitude <= 0.001f;

        if (!isBackstep)
        {
            owner.transform.rotation = UnityEngine.Quaternion.LookRotation(moveInputDirection.normalized);
        }

        // 2. 스태미나 차감
        _playerStatController?.ConsumeStamina(RollStaminaCost);

        // 3. 기존 잔여 관성 속도 초기화 (이전 이동 속도가 구르기에 간섭하는 것 방지)
        owner.StopMovementAPI();

        // 4. Root Motion 활성화 (클립의 전진 변위가 Rigidbody를 직접 추진하도록 권한 부여)
        _playerAnimController.SetRootMotion(true);

        // 5. 애니메이터 Roll 트리거 실행
        _playerAnimController.SetTrigger(AnimParamType.Roll);

        // 6. 무적 시간 및 상태 복귀 비동기 타이머 가동
        System.Threading.CancellationToken token = CreateActionToken();
        ExecuteRollAsync(isBackstep, token).Forget();
    }

    public override void Exit()
    {
        base.Exit();
        // 구르기가 종료되면 Root Motion을 회수하여 일반 이동 제어권 복원
        _playerAnimController.SetRootMotion(false);
        owner.StopMovementAPI();
        IsInvincible = false;
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid ExecuteRollAsync(bool isBackstep, System.Threading.CancellationToken token)
    {
        // 백스탭은 무적 시간이 짧음 (0.15초), 일반 구르기는 0.35초
        float actualInvincible = isBackstep ? 0.15f : InvincibleDuration;
        IsInvincible = true;

        // 1. 무적 시간 대기
        bool isCanceledAtInvincible = await Cysharp.Threading.Tasks.UniTask.Delay((int)(InvincibleDuration * 1000f),
            cancellationToken: token).SuppressCancellationThrow();
        if (isCanceledAtInvincible) return;

        IsInvincible = false;

        // 2. 잔여 후딜레이 대기
        float remainDelay = RollTotalDuration - InvincibleDuration;
        bool isCanceledAtFinish = await Cysharp.Threading.Tasks.UniTask.Delay((int)(remainDelay * 1000f),
            cancellationToken: token).SuppressCancellationThrow();
        if (isCanceledAtFinish) return;

        // 3. 다음 상태 전이 콜백
        CompleteActionAndEvaluateTransition();
    }
}
