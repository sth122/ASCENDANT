/// <summary>
/// 특수 이벤트(메테오 샤워 풀링 소환)가 결합된 공중 폭격 스킬
/// </summary>
[UnityEngine.CreateAssetMenu(fileName = "Dragon_AerialCombo", menuName = "Unit/Skill/Dragon/AerialComboSkill")]
public class DragonAerialComboSkillSO : BossSkillDataSO
{
    public override BossSkill CreateSkill(BossBTController owner)
    {
        return new TimelineBossSkill(owner, _info, _timelineSteps.ToArray(), () => SpawnMeteorShowerAsync(owner));
    }

    private Cysharp.Threading.Tasks.UniTask SpawnMeteorShowerAsync(BossBTController owner)
    {
        if (owner.CurrentTarget != null && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Spawn(
                PoolType.MeteorShower,
                owner.CurrentTarget.position,
                UnityEngine.Quaternion.identity
            );
        }
        return Cysharp.Threading.Tasks.UniTask.CompletedTask;
    }
}