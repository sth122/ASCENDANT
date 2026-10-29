public abstract class BossSkill : UnitSkill
{
    protected readonly BossBTController Owner;
    protected BossSkill(BossBTController owner, in SkillInfo info) : base(info) => Owner = owner;

    /// <summary>
    /// 쿨타임 충족 여부뿐 아니라 현재 보스의 페이즈 조건을 스스로 평가
    /// </summary>
    public override bool CanExecute()
    {
        // 쿨타임 검사
        if (!base.CanExecute()) return false;

        // 소유 Unit 또는 블랙보드 참조 검증
        if(Owner == null || Owner.Blackboard == null) return true;

        // 페이즈 조건 검사: 2페이즈 전용 스킬인데 아직 1페이즈라면 실행 불가
        int currentPhase = Owner.Blackboard.IsPhase2 ? 2 : 1;
        if (Info.RequiredPhase > currentPhase) return false;

        return true;
    }
}