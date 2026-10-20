/// <summary>
/// 크리처 전용 기상 수색 상태 (UnitState.Alert <-> CreatureAlertState)
/// </summary>
public class CreatureAlertState : EnemyBaseState
{
    private UnityEngine.Vector3 _alertNoisePosition;
    private bool _hasNoiseTarget = false;
    private const float TurnSpeed = 4.0f;

    public CreatureAlertState(CreatureEnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        owner.StopMovementAPI();

        System.Threading.CancellationToken token = CreateStateToken();
        ExecuteAlertSequenceAsync(token).Forget();
    }

    public override void Update()
    {
        base.Update();

        // 소음 진원지가 있다면 Update 루프에서 점진적으로 부드럽게 회전
        if (_hasNoiseTarget)
        {
            UnityEngine.Vector3 lookDir = _alertNoisePosition - owner.transform.position;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.01f)
            {
                UnityEngine.Quaternion targetRot = UnityEngine.Quaternion.LookRotation(lookDir.normalized);
                owner.transform.rotation = UnityEngine.Quaternion.Slerp(owner.transform.rotation, targetRot, TurnSpeed * UnityEngine.Time.deltaTime);

                // 목표 각도에 근접하면 회전 완료
                if (UnityEngine.Quaternion.Angle(owner.transform.rotation, targetRot) < 2.0f)
                {
                    _hasNoiseTarget = false;
                }
            }
        }
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid ExecuteAlertSequenceAsync(System.Threading.CancellationToken token)
    {
        // 1. 포복에서 서기 전환 모션
        _enemyAnimController.PlayState(AnimStateType.CrawlToState);

        bool isCanceled = await Cysharp.Threading.Tasks.UniTask.Delay(1200,
            cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // 2. 서서 대기 모션
        _enemyAnimController.PlayState(AnimStateType.BattleIdle);

        // 3. 5초간 수색 대기
        isCanceled = await Cysharp.Threading.Tasks.UniTask.Delay(5000, 
            cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // 4. 서기에서 포복 복귀 모션
        _enemyAnimController.PlayState(AnimStateType.StateToCrawl);

        isCanceled = await Cysharp.Threading.Tasks.UniTask.Delay(1200, 
            cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // 5. 포복 순찰 복귀
        _enemyStateMachine.ChangeState((int)UnitState.Patrol);
    }
    public void SetNoiseTargetPosition(UnityEngine.Vector3 noisePosition)
    {
        _alertNoisePosition = noisePosition;
        _hasNoiseTarget = true;
    }
}