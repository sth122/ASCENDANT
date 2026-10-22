using UnityEngine;

/// <summary>
/// Enemy 사망 상태를 담당하는 FSM 상태 클래스
/// </summary>
public class EnemyDieState : EnemyBaseState
{
    public EnemyDieState(EnemyController owner, EnemyStateMachine stateMachine)
        : base(owner, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
        owner.StopMovementAPI();

        AnimStateType selectedDeathMotion = AnimStateHelper.GetRandomDeathState();
        _enemyAnimController.PlayState(selectedDeathMotion);

        owner.Perception.StopSensorLoop();

        if (owner.Collider != null && owner.Rigidbody != null)
        {
            owner.Rigidbody.isKinematic = true;
            owner.Collider.isTrigger = true;
        }

        if (owner.Movement.Agent != null)
        {
            owner.SetAgentableAPI(false);
        }

        // 전역 탐색(FindAnyObjectByType) 제거: IPlayerProvider 서비스를 통해 안전하게 소울 지급
        DistributeRewardSouls();

        owner.enabled = false;
    }

    private void DistributeRewardSouls()
    {
        if (owner.StatusController == null) return;

        int rewardSouls = owner.StatusController.DropSouls;
        if (rewardSouls <= 0) return;

        if (ServiceLocator.TryGet<IPlayerProvider>(out IPlayerProvider playerProvider) && playerProvider.IsPlayerSpawned)
        {
            PlayerController player = playerProvider.Player;
            if (player != null && player.StatusController != null && player.StatusController.PlayerStatusSource != null)
            {
                player.StatusController.PlayerStatusSource.AddSouls(rewardSouls);
                DebugLogger.LogWithTag("EnemyDieState", $"[처치 보상 지급 완료] +{rewardSouls} Souls");
            }
        }
    }
}
