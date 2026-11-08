using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 에너미의 강인도 소진(Groggy), 패링 스태거, 플레이어 처형(앞잡) 피격 및 자연 기상 시퀀스를 전담하는 모듈
/// </summary>
public class EnemyGroggyModule : UnitBaseModule, IDisposable
{
    private readonly EnemyBTController _owner;
    private readonly EnemyStatusController _status;
    private readonly EnemyAnimController _anim;
    private readonly EnemyMovement _movement;
    private readonly float _groggyDuration;

    private CancellationTokenSource _groggyCts;
    public bool IsGroggy { get; private set; } = false;
    public bool CanBeRiposted { get; private set; } = false;

    public EnemyGroggyModule(EnemyBTController owner, EnemyStatusController status, EnemyAnimController anim, EnemyMovement movement, float groggyDuration = 8.0f)
    {
        _owner = owner;
        _status = status;
        _anim = anim;
        _movement = movement;
        _groggyDuration = groggyDuration;
    }

    public void StartGroggy()
    {
        if (IsGroggy || _owner.IsDead) return;

        IsGroggy = true;
        CanBeRiposted = true;

        _movement.StopMovement();
        _owner.CombatController.CloseAllHitboxes();
        _owner.ReleaseAttackToken();

        _status?.SetPoiseRecoveryPause(true);

        AsyncUtil.CancelAndDispose(ref _groggyCts);
        _groggyCts = CancellationTokenSource.CreateLinkedTokenSource(_owner.destroyCancellationToken);

        RunGroggyTimerAsync(_groggyCts.Token).Forget();
    }

    private async UniTaskVoid RunGroggyTimerAsync(CancellationToken token)
    {
        float elapsed = 0f;
        try
        {
            // 크리처는 Groggy_Loop 대신 Hit1 모션 재생
            AnimStateType groggyState = _owner is CreatureBT ? AnimStateType.Hit1 : AnimStateType.Groggy_Loop;
            _anim.PlayState(groggyState, force: true);

            while (elapsed < _groggyDuration)
            {
                bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (canceled) return;
                elapsed += Time.deltaTime;
            }
        }
        catch (OperationCanceledException) { return; }

        RecoverNaturally();
    }

    public void RecoverNaturally()
    {
        CanBeRiposted = false;
        IsGroggy = false;
        _status?.RestorePoiseToMax();
        _status?.SetPoiseRecoveryPause(false);
        _anim.PlayState(AnimStateType.Idle, force: true);
    }

    public void HandleRiposted()
    {
        _owner.ReleaseAttackToken();
        _movement.StopMovement();
        CanBeRiposted = false;
        _owner.CombatController.CloseAllHitboxes();

        AsyncUtil.CancelAndDispose(ref _groggyCts);

        _anim.PlayState(AnimStateType.Hit2, force: true);
    }

    public async UniTask ExecuteRiposteWakeUpAsync(CancellationToken token)
    {
        CanBeRiposted = false;
        try
        {
            await _owner.PlayStateAndWaitAsync(AnimStateType.Hit2, token, force: true, customDuration: 2.5f);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _status?.RestorePoiseToMax();
            _status?.SetPoiseRecoveryPause(false);
            IsGroggy = false;
            CanBeRiposted = false;
            _anim.PlayState(AnimStateType.Idle, force: true);
        }
    }

    public void ResetState()
    {
        AsyncUtil.CancelAndDispose(ref _groggyCts);
        IsGroggy = false;
        CanBeRiposted = false;
    }

    public void Dispose() => AsyncUtil.CancelAndDispose(ref _groggyCts);
}