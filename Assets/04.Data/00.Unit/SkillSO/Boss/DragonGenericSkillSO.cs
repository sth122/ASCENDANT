/// <summary>
/// 범용 타임라인 보스 스킬 SO (후퇴 브레스, 돌진, 꼬리치기, 직선 브레스 등 인스펙터 리스트 편집 지원)
/// </summary>
[UnityEngine.CreateAssetMenu(fileName = "Dragon_GenericSkill", menuName = "Unit/Skill/Dragon/GenericSkillSO")]
public class DragonGenericSkillSO : BossSkillDataSO
{
    public override BossSkill CreateSkill(BossBTController owner)
    {
        return new TimelineBossSkill(owner, _info, _timelineSteps.ToArray());
    }
}