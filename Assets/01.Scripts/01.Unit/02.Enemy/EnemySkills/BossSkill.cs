public abstract class BossSkill : UnitSkill
{
    protected readonly BossBTController Owner;
    protected BossSkill(BossBTController owner, in SkillInfo info) : base(info) => Owner = owner;

    /// <summary>
    /// 쿨타임 및 보스 페이즈 조건 검사
    /// </summary>
    public override bool CanExecute()
    {
        if (!base.CanExecute()) return false;
        if (Owner == null || Owner.Blackboard == null) return true;

        int currentPhase = Owner.Blackboard.IsPhase2 ? 2 : 1;
        if (Info.RequiredPhase > currentPhase) return false;

        return true;
    }

    /// <summary>
    /// 쿨타임, 페이즈 및 타깃과의 사거리(Range) 적합성 검사
    /// </summary>
    public virtual bool CanExecute(UnityEngine.Transform target)
    {
        if (!CanExecute()) return false;
        if (target == null) return false;

        UnityEngine.Vector3 diff = target.position - Owner.transform.position;
        diff.y = 0f;
        float distSqr = diff.sqrMagnitude;
        float range = Info.Range > 0f ? Info.Range : 6.0f;

        return distSqr <= (range * range);
    }
}