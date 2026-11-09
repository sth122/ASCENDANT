using Cysharp.Threading.Tasks;
using System;
using System.Threading;
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
    public event Action OnGroggyEnded;
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

        AsyncUtil.CancelAndDispose(ref _groggyCts);
        _groggyCts = CancellationTokenSource.CreateLinkedTokenSource(_owner.destroyCancellationToken);

        RunGroggyTimerAsync(_groggyCts.Token).Forget();
    }

    private async UniTaskVoid RunGroggyTimerAsync(CancellationToken token)
    {
        float elapsed = 0f;
        try
        {
            _anim.PlayState(AnimStateType.Groggy_Loop, force: true);

            // 1. 그로기 타이머 완주 루프
            while (elapsed < _groggyDuration)
            {
                bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (canceled) return;

                elapsed += Time.deltaTime;

                float remainingRatio = Mathf.Clamp01(1f - (elapsed / _groggyDuration));
                _status?.SetGroggyCountdownPoise(remainingRatio);
            }
        }
        catch (OperationCanceledException) { return; }
        EndGroggy();
    }

    public void HandleRiposted()
    {
        CanBeRiposted = false;

        _owner.ReleaseAttackToken();
        _movement.StopMovement();
        _owner.CombatController.CloseAllHitboxes();
    }

    /// <summary>
    /// CurrentPoise = 0 도달 시 최종 정리 및 복귀
    /// </summary>
    private void EndGroggy()
    {
        IsGroggy = false;
        CanBeRiposted = false;

        // 사망한 경우 전투로 복귀하지 않음
        if (_owner.IsDead || (_status != null && _status.CurrentHp <= 0f)) return;

        // 포이즈 0으로 초기화
        _status?.ResetPoiseToDefault();

        // 보스 컨트롤러 및 상위 시스템에 그로기 종료 통지
        OnGroggyEnded?.Invoke();

        // 상위 컨트롤러가 후속 연출을 점유하지 않은 경우에만 기본 Idle 복귀
        if (_owner.ActionState == EnemyActionState.Groggy)
        {
            _anim.PlayState(AnimStateType.Idle, force: true);
            _owner.SetActionState(EnemyActionState.None);
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