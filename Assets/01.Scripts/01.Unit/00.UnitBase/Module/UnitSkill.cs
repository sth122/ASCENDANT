using System.Threading;
using Cysharp.Threading.Tasks;

public abstract class UnitSkill
{
    public SkillInfo Info { get; }
    protected float _nextAvailableTime = 0f;

    public bool IsCooldownReady => UnityEngine.Time.time >= _nextAvailableTime;

    protected UnitSkill(in SkillInfo info) => Info = info;

    public virtual bool CanExecute() => IsCooldownReady;
    public virtual void TriggerCooldown() => _nextAvailableTime = UnityEngine.Time.time + Info.Cooldown;
    public virtual void ResetCooldown() => _nextAvailableTime = 0f;

    public abstract UniTask<bool> ExecuteAsync(CancellationToken ct);
}