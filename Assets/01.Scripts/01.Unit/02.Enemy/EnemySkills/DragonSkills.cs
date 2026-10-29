using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 후퇴 좌우 부채꼴 브레스
/// </summary>
public class DragonBackstepBreathSkill : BossSkill
{
    public DragonBackstepBreathSkill(BossBTController owner, in SkillInfo info) : base(owner, info) { }

    public override async UniTask<bool> ExecuteAsync(CancellationToken ct)
    {
        float speedMod = Owner.Blackboard.IsPhase2 ? Owner.Blackboard.Phase2StatBuffMultiplier : 1.0f;
        try
        {
            // 1. 후방 후퇴 모션
            Owner.AnimController.PlayState(AnimStateType.Backstep);
            await UniTask.Delay(TimeSpan.FromSeconds(2.8f / speedMod), cancellationToken: ct);

            // 2. 후퇴 위치에서 좌우로 쓸어 넘기는 부채꼴 화염 브레스 방사
            Owner.AnimController.PlayState(AnimStateType.Dragon_BackstepBreath);
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f / speedMod), cancellationToken: ct);

            // 3. Mouth 히트박스 오픈 
            Owner.OpenHitboxSlot(Info.TargetHitboxSlot, Info.BaseDamage, Info.BasePoiseDamage,
                Info.IsParryable, Info.ImpactType, Info.KnockbackForce);

            // 4. 좌우 브레스 화염 방사 지속 시간 대기
            await UniTask.Delay(TimeSpan.FromSeconds(2.6f / speedMod), cancellationToken: ct);
        }
        finally
        {
            Owner.CloseHitboxSlot(Info.TargetHitboxSlot);
        }

        // 5. 후딜레이 복귀 대기
        await UniTask.Delay(TimeSpan.FromSeconds(1.2f / speedMod), cancellationToken: ct);
        return true;
    }
}

/// <summary>
/// 후퇴 돌진 박치기
/// </summary>
public class DragonBackstepChargeSkill : BossSkill
{
    public DragonBackstepChargeSkill(BossBTController owner, in SkillInfo info) : base(owner, info) { }

    public override async UniTask<bool> ExecuteAsync(CancellationToken ct)
    {
        float speedMod = Owner.Blackboard.IsPhase2 ? Owner.Blackboard.Phase2StatBuffMultiplier : 1.0f;

        try
        {
            // 1. 후방 후퇴 모션
            Owner.AnimController.PlayState(AnimStateType.Backstep);
            await UniTask.Delay(TimeSpan.FromSeconds(2.8f / speedMod), cancellationToken: ct);

            // 2. 돌진 개시 전 플레이어 방향으로 즉시 선회
            if (Owner.CurrentTarget != null)
            {
                Owner.Movement.FaceTarget(Owner.CurrentTarget.position, 15f, Time.deltaTime);
            }

            // 3. 전신 돌진 박치기 모션
            Owner.AnimController.PlayState(AnimStateType.BackstepCharge);
            await UniTask.Delay(TimeSpan.FromSeconds(0.7f / speedMod), cancellationToken: ct);

            // 4. WholeBody 히트박스 오픈
            Owner.OpenHitboxSlot(Info.TargetHitboxSlot, Info.BaseDamage, Info.BasePoiseDamage,
                Info.IsParryable, Info.ImpactType, Info.KnockbackForce);

            // 돌진 액티브 판정 지속
            await UniTask.Delay(TimeSpan.FromSeconds(1.3f / speedMod), cancellationToken: ct);
        }
        finally
        {
            Owner.CloseHitboxSlot(Info.TargetHitboxSlot);
        }

        // 5. 돌진 후 정지 후딜레이
        await UniTask.Delay(TimeSpan.FromSeconds(1.0f / speedMod), cancellationToken: ct);
        return true;
    }
}

/// <summary>
/// 꼬리 360도 회전 - 단일 애니메이션
/// </summary>
public class DragonTailSweepSkill : BossSkill
{
    public DragonTailSweepSkill(BossBTController owner, in SkillInfo info) : base(owner, info) { }

    public override async UniTask<bool> ExecuteAsync(CancellationToken ct)
    {
        float speedMod = Owner.Blackboard.IsPhase2 ? Owner.Blackboard.Phase2StatBuffMultiplier : 1.0f;

        try
        {
            // 1. 360도 회전 꼬리치기
            Owner.AnimController.PlayState(Info.AnimState);
            await UniTask.Delay(TimeSpan.FromSeconds(0.6f / speedMod), cancellationToken: ct);

            // 2. Tail 히트박스 오픈
            Owner.OpenHitboxSlot(Info.TargetHitboxSlot, Info.BaseDamage, Info.BasePoiseDamage,
                Info.IsParryable, Info.ImpactType, Info.KnockbackForce);

            await UniTask.Delay(TimeSpan.FromSeconds(0.8f / speedMod), cancellationToken: ct);
        }
        finally
        {
            Owner.CloseHitboxSlot(Info.TargetHitboxSlot);
        }

        // 3. 회전 후 복귀 딜레이
        await UniTask.Delay(TimeSpan.FromSeconds(0.6f / speedMod), cancellationToken: ct);
        return true;
    }
}

/// <summary>
/// 전방 직선 관통 브레스 - 단일 애니메이션
/// </summary>
public class DragonLinearBreathSkill : BossSkill
{
    public DragonLinearBreathSkill(BossBTController owner, in SkillInfo info) : base(owner, info) { }

    public override async UniTask<bool> ExecuteAsync(CancellationToken ct)
    {
        float speedMod = Owner.Blackboard.IsPhase2 ? Owner.Blackboard.Phase2StatBuffMultiplier : 1.0f;

        try
        {
            // 1. 직선형 관통 브레스
            Owner.AnimController.PlayState(Info.AnimState);
            await UniTask.Delay(TimeSpan.FromSeconds(0.8f / speedMod), cancellationToken: ct);

            // 2. Mouth 히트박스 오픈 
            Owner.OpenHitboxSlot(Info.TargetHitboxSlot, Info.BaseDamage, Info.BasePoiseDamage,
                Info.IsParryable, Info.ImpactType, Info.KnockbackForce);

            await UniTask.Delay(TimeSpan.FromSeconds(2.0f / speedMod), cancellationToken: ct);
        }
        finally
        {
            Owner.CloseHitboxSlot(Info.TargetHitboxSlot);
        }

        // 5. 후딜레이 
        await UniTask.Delay(TimeSpan.FromSeconds(1.0f / speedMod), cancellationToken: ct);
        return true;
    }
}

/// <summary>
/// 2페이즈 전용 공중 폭격 콤보
public class DragonAerialComboSkill : BossSkill
{
    public DragonAerialComboSkill(BossBTController owner, in SkillInfo info)
        : base(owner, info) { }

    public override async UniTask<bool> ExecuteAsync(CancellationToken ct)
    {
        float speedMod = Owner.Blackboard.Phase2StatBuffMultiplier;

        try
        {
            // 1. 후방 후퇴 모션
            Owner.AnimController.PlayState(AnimStateType.Backstep);
            await UniTask.Delay(TimeSpan.FromSeconds(2.4f / speedMod), cancellationToken: ct);

            // 2. 후퇴 위치에서 공중 도약
            Owner.AnimController.PlayState(AnimStateType.Dragon_Takeoff_dz);
            await UniTask.Delay(TimeSpan.FromSeconds(1.5f / speedMod), cancellationToken: ct);

            // 3. 공중 체공 중 브레스 방사
            Owner.AnimController.PlayState(AnimStateType.Dragon_AerialBreath_dz);

            // 4. 메테오 샤워 스폰
            SpawnMeteorShowerFromPool();

            await UniTask.Delay(TimeSpan.FromSeconds(0.8f / speedMod), cancellationToken: ct);

            // 5. Mouth 히트박스 오픈
            Owner.OpenHitboxSlot(Info.TargetHitboxSlot, Info.BaseDamage, Info.BasePoiseDamage,
                Info.IsParryable, Info.ImpactType, Info.KnockbackForce);

            // 브레스 방사 유지 대기
            await UniTask.Delay(TimeSpan.FromSeconds(2.8f / speedMod), cancellationToken: ct);
        }
        finally
        {
            Owner.CloseHitboxSlot(Info.TargetHitboxSlot);
        }

        // 6. 지면 강습 착지
        Owner.AnimController.PlayState(AnimStateType.Dragon_Landing_dz);
        await UniTask.Delay(TimeSpan.FromSeconds(1.6f / speedMod), cancellationToken: ct);

        return true;
    }

    private void SpawnMeteorShowerFromPool()
    {
        if (Owner.CurrentTarget != null && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Spawn(PoolType.MeteorShower,Owner.CurrentTarget.position,Quaternion.identity);
        }
    }
}