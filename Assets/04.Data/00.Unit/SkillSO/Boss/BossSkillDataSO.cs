using Cysharp.Threading.Tasks;

public abstract class BossSkillDataSO : UnityEngine.ScriptableObject
{
    [UnityEngine.SerializeField] protected SkillInfo _info;
    [UnityEngine.Tooltip("단일 모션은 1개, 복합 시퀀스는 순서대로 단계를 추가")]
    [UnityEngine.SerializeField] 
    protected System.Collections.Generic.List<ActionTimelineStep> _timelineSteps 
        = new System.Collections.Generic.List<ActionTimelineStep>();


    public SkillInfo Info => _info;
    public System.Collections.Generic.IReadOnlyList<ActionTimelineStep> TimelineSteps => _timelineSteps;

    public abstract BossSkill CreateSkill(BossBTController owner);
}

/// <summary>
/// BossSkillDataSO의 타임라인 단계를 ActionTimelineRunner로 구동하는 범용 런타임 스킬 클래스
/// </summary>
public class TimelineBossSkill : BossSkill
{
    protected readonly ActionTimelineStep[] CachedSteps;
    protected readonly System.Func<BossBTController, UniTask> CustomEventCallback;

    public TimelineBossSkill(BossBTController owner, in SkillInfo info, ActionTimelineStep[] steps, System.Func<BossBTController, UniTask> customEvent = null)
        : base(owner, info)
    {
        CachedSteps = steps;
        CustomEventCallback = customEvent;
    }

    public override async UniTask<bool> ExecuteAsync(System.Threading.CancellationToken ct)
    {
        float speedMod = Owner.Blackboard.IsPhase2 ? Owner.Blackboard.Phase2StatBuffMultiplier : 1.0f;

        ActionTimelineRunner.ExecutionContext ctx = new ActionTimelineRunner.ExecutionContext
        {
            AnimController = Owner.AnimController,
            Movement = Owner.Movement,
            Target = Owner.CurrentTarget,
            GlobalSpeedMultiplier = speedMod,
            OnOpenHitbox = Owner.OpenHitboxSlot,
            OnCloseHitbox = Owner.CloseHitboxSlot,
            OnCloseAllHitboxes = Owner.CloseAllHitboxes,
            OnCustomEventTrigger = CustomEventCallback,
            BaseDamage = Info.BaseDamage,
            BasePoiseDamage = Info.BasePoiseDamage,
            IsParryable = Info.IsParryable,
            ImpactType = Info.ImpactType,
            KnockbackForce = Info.KnockbackForce
        };

        return await ActionTimelineRunner.ExecuteTimelineAsync(CachedSteps, ctx, ct);
    }
}