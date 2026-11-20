using UnityEngine;

/// <summary>
/// 플레이어가 닿았을 때 즉사(치명적인 대미지 또는 강제 사망)를 유발하는 트리거 컴포넌트
/// (낭떠러지 바닥, 용암, 즉사 함정에 부착하여 사용)
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeathTrigger : MonoBehaviour
{
    private void Awake()
    {
        // 닿았을 때 감지할 수 있도록 콜라이더의 IsTrigger를 강제로 true 보장
        if (TryGetComponent(out Collider col))
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. 플레이어 컨트롤러 컴포넌트 탐색 (부모 객체 포함)
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        // 2. 이미 사망한 상태라면 중복 처리 방지
        if (player.StatusController != null && player.StatusController.IsDead) return;


            // 강제 사망 이벤트 발행 및 FSM Die 상태 전이
        ForceKillPlayer(player);
    }

    private void ForceKillPlayer(PlayerController player)
    {
        // 1. 체력을 0으로 직접 설정하고 사망 이벤트 트리거
        if (player.StatusController != null)
        {
            player.StatusController.SetHpDirectly(0f);
        }

        // 2. FSM 상태를 Die로 강제 전이
        if (player.StateMachineInstance != null)
        {
            player.StateMachineInstance.ChangeState(UnitState.Die, forceReenter: true);
        }
    }
}