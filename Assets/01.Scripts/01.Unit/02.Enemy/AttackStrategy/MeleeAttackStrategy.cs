/// <summary>
/// 본체 공격력(EnemyStat)과 모션 배율(EnemyAttackData)을 결합하여 히트박스를 여는 근접 공격 전략 클래스
/// </summary>
public class MeleeAttackStrategy : IEnemyAttackStrategy
{
    private EnemyAttackContext _activeContext;
    private EnemyAttackData _activeData;
    private bool _isHitboxOpen = false;

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
        _isHitboxOpen = false;

        // 1. 타겟 정면 응시 회전
        context.Movement.FaceTarget(target.position, 15.0f, UnityEngine.Time.deltaTime);

        // 2. 애니메이션 상태 재생
        context.AnimController.PlayState(data.AttackAnimStateType);
    }

    public void Update(in EnemyAttackContext context, UnityEngine.Transform target, in EnemyAttackData data, float normalizedTime)
    {
        // 1. 선딜레이(히트박스 열리기 전) 동안 플레이어를 유연하게 축 추적
        if (normalizedTime < data.HitboxStartNormalized && target != null)
        {
            context.Movement.FaceTarget(target.position, 8.0f, UnityEngine.Time.deltaTime);
        }

        // 2. 히트박스 활성화 구간 판정
        if (!_isHitboxOpen && normalizedTime >= data.HitboxStartNormalized && normalizedTime < data.HitboxEndNormalized)
        {
            OpenHitbox(in context, in data);
        }
        // 3. 히트박스 닫힘 구간 판정
        else if (_isHitboxOpen && normalizedTime >= data.HitboxEndNormalized)
        {
            CloseHitbox(in context);
        }
    }

    public void Stop(in EnemyAttackContext context)
    {
        CloseHitbox(in context);
        _activeData = default;
        _isHitboxOpen = false;
    }

    private void OpenHitbox(in EnemyAttackContext context, in EnemyAttackData data)
    {
        if (context.MainHitbox == null) return;
        _isHitboxOpen = true;

        float finalDamage = data.CalculateFinalDamage(context.BaseAttackPower);
        float finalPoiseDamage = data.CalculateFinalPoiseDamage(context.BasePoiseDamage);

        context.MainHitbox.OpenHitBox(context.AttackerObject, finalDamage, finalPoiseDamage, data.IsParryable);
    }

    private void CloseHitbox(in EnemyAttackContext context)
    {
        if (!_isHitboxOpen) return;
        _isHitboxOpen = false;

        if (context.MainHitbox != null)
        {
            context.MainHitbox.CloseHitBox();
        }
    }
}
