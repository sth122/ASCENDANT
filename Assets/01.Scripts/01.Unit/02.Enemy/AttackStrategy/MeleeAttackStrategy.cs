/// <summary>
/// 본체 공격력(EnemyStat)과 모션 배율(EnemyAttackData)을 결합하여 히트박스를 여는 근접 공격 전략 클래스
/// </summary>
public class MeleeAttackStrategy : IEnemyAttackStrategy
{
    private EnemyAttackContext _activeContext;
    private EnemyAttackData _activeData;
    public bool CanExecute(in EnemyAttackContext context, UnityEngine.Transform target, in EnemyAttackData data)
    {
        if (target == null || context.OwnerTransform == null) return false;

        UnityEngine.Vector3 toTarget = target.position - context.OwnerTransform.position;
        toTarget.y = 0f;

        float sqrDistance = toTarget.sqrMagnitude;
        float sqrRange = data.AttackRange * data.AttackRange;

        return sqrDistance <= sqrRange;
    }

    public void Execute(in EnemyAttackContext context, UnityEngine.Transform target, in EnemyAttackData data)
    {
        _activeContext = context;
        _activeData = data;

        // 1. 타겟 정면 응시 회전
        context.Movement.FaceTarget(target.position, 15.0f, UnityEngine.Time.deltaTime);

        // 2. 애니메이션 상태 재생
        context.AnimController.PlayState(data.AttackAnimStateType);

        // 3. 히트박스 이벤트 구독
        SubsribeEvents();
    }

    public void Stop(in EnemyAttackContext context)
    {
        HandleHitboxClose();
        UnsubscribeEvents();
        _activeData = default;
    }

    private void HandleHitboxOpen()
    {
        if (_activeContext.MainHitbox != null)
        {
            // 본체 스탯과 모션 배율의 결합 연산 수행
            float finalDamage = _activeData.CalculateFinalDamage(_activeContext.BaseAttackPower);
            float finalPoiseDamage = _activeData.CalculateFinalPoiseDamage(_activeContext.BasePoiseDamage);

            _activeContext.MainHitbox.OpenHitBox(_activeContext.AttackerObject, finalDamage, finalPoiseDamage,
                _activeData.IsParryable);
        }
    }

    private void HandleHitboxClose()
    {
        if (_activeContext.MainHitbox != null)
        {
            _activeContext.MainHitbox.CloseHitBox();
        }
    }

    private void SubsribeEvents()
    {
        UnsubscribeEvents();
        if (_activeContext.AnimController != null)
        {
            _activeContext.AnimController.OnHitboxOpenEvent += HandleHitboxOpen;
            _activeContext.AnimController.OnHitboxCloseEvent += HandleHitboxClose;
        }
    }

    private void UnsubscribeEvents()
    {
        if (_activeContext.AnimController != null)
        {
            _activeContext.AnimController.OnHitboxOpenEvent -= HandleHitboxOpen;
            _activeContext.AnimController.OnHitboxCloseEvent -= HandleHitboxClose;
        }
    }
}
