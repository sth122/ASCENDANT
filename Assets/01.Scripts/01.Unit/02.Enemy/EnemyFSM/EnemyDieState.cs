using UnityEngine;

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

        // 임시 FindAny 소울 드랍
        DistributeRewardSouls();

        owner.enabled = false;
    }

    private void DistributeRewardSouls()
    {
        if (owner.StatusController == null) return;

        int rewardSouls = owner.StatusController.DropSouls;
        if (rewardSouls <= 0) return;

        // 플레이어 탐색 후 소울 지급
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null && player.StatusController != null && player.StatusController.PlayerStatusSource != null)
        {
            player.StatusController.PlayerStatusSource.AddSouls(rewardSouls);
            DebugLogger.LogWithTag("EnemyDieState", $"처치 보상 소울 지급 완료: +{rewardSouls} Souls");
        }
    }
}
