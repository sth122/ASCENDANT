using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 단일 및 복합 액션 타임라인을 파싱하여 비동기로 순차 실행하는 엔진
/// </summary>
public static class ActionTimelineRunner
{
    public struct ExecutionContext
    {
        public UnitAnimController AnimController;
        public UnitMovement Movement;
        public Transform Target;
        public float GlobalSpeedMultiplier; // 2페이즈 광폭화 배속 등 (기본 1.0)

        // 히트박스 제어 대리자
        public Action<HitboxSlot, float, float, bool, AttackImpactType, float> OnOpenHitbox;
        public Action<HitboxSlot> OnCloseHitbox;
        public Action OnCloseAllHitboxes;

        // 특수 기믹 실행 대리자
        public Func<BossBTController, UniTask> OnCustomEventTrigger;
        public BossBTController BossOwner;

        // 공격 대미지 파라미터
        public float BaseDamage;
        public float BasePoiseDamage;
        public bool IsParryable;
        public AttackImpactType ImpactType;
        public float KnockbackForce;
    }

    /// <summary>
    /// N개의 Step으로 이루어진 타임라인 시퀀스를 완전 비동기 실행
    /// </summary>
    public static async UniTask<bool> ExecuteTimelineAsync(ActionTimelineStep[] steps, ExecutionContext ctx, CancellationToken ct)
    {
        if (steps == null || steps.Length == 0) return false;

        try
        {
            for (int i = 0; i < steps.Length; i++)
            {
                if (ct.IsCancellationRequested) return false;

                ActionTimelineStep step = steps[i];
                await ExecuteSingleStepAsync(step, ctx, ct);
            }
        }
        finally
        {
            // 인터럽트나 완료 시 히트박스 강제 닫기 보장
            ctx.OnCloseAllHitboxes?.Invoke();
        }

        return true;
    }

    private static async UniTask ExecuteSingleStepAsync(ActionTimelineStep step, ExecutionContext ctx, CancellationToken ct)
    {
        // 1. 타깃 선회
        if (step.FaceTargetOnEnter && ctx.Target != null && ctx.Movement != null)
        {
            ctx.Movement.SnapToTarget(ctx.Target.position);
        }

        // 2. 모션 재생
        ctx.AnimController.PlayState(step.StateType, force: true);

        // 3. 실제 애니메이션 클립 길이 및 유효 배속 연산
        float clipDuration = ctx.AnimController.GetStateDuration(step.StateType);
        float stepSpeed = Mathf.Max(0.1f, (step.SpeedMultiplier > 0 ? step.SpeedMultiplier : 1.0f) * ctx.GlobalSpeedMultiplier);
        float actualDuration = clipDuration / stepSpeed;

        // 4-A. 타격 및 특수 이벤트가 없는 단순 전환/이동 단계인 경우 전체 시간 대기
        if (!step.HasHitbox && !step.HasCustomEvent)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(actualDuration), cancellationToken: ct);
            return;
        }

        // 4-B. 타격 또는 특수 이벤트가 포함된 정밀 타임라인 실행
        await ExecutePreciseStepTimelineAsync(step, actualDuration, ctx, ct);
    }

    private static async UniTask ExecutePreciseStepTimelineAsync(ActionTimelineStep step, float totalDuration, ExecutionContext ctx, CancellationToken ct)
    {
        float startupTime = totalDuration * Mathf.Clamp01(step.HitboxStartNormalized);
        float activeDuration = totalDuration * Mathf.Clamp01(step.HitboxEndNormalized - step.HitboxStartNormalized);
        float recoveryTime = totalDuration * Mathf.Clamp01(1.0f - step.HitboxEndNormalized);

        float eventTriggerTime = step.HasCustomEvent ? totalDuration * Mathf.Clamp01(step.EventTriggerNormalized) : -1f;
        bool isEventTriggered = false;

        // [선딜레이 대기]
        if (startupTime > 0f)
        {
            if (eventTriggerTime >= 0f && eventTriggerTime <= startupTime)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(eventTriggerTime), cancellationToken: ct);
                ctx.OnCustomEventTrigger?.Invoke(ctx.BossOwner).Forget();
                isEventTriggered = true;
                await UniTask.Delay(TimeSpan.FromSeconds(startupTime - eventTriggerTime), cancellationToken: ct);
            }
            else
            {
                await UniTask.Delay(TimeSpan.FromSeconds(startupTime), cancellationToken: ct);
            }
        }

        try
        {
            // [히트박스 오픈]
            if (step.HasHitbox)
            {
                ctx.OnOpenHitbox?.Invoke(step.TargetHitboxSlot, ctx.BaseDamage, ctx.BasePoiseDamage,
                    ctx.IsParryable, ctx.ImpactType, ctx.KnockbackForce);
            }

            if (step.HasCustomEvent && !isEventTriggered)
            {
                ctx.OnCustomEventTrigger?.Invoke(ctx.BossOwner).Forget();
            }

            if (activeDuration > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(activeDuration), cancellationToken: ct);
            }
        }
        finally
        {
            // 타격 구간 종료 시 즉시 히트박스 닫기
            if (step.HasHitbox)
            {
                ctx.OnCloseHitbox?.Invoke(step.TargetHitboxSlot);
            }
        }

        // [후딜레이 대기]
        if (recoveryTime > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(recoveryTime), cancellationToken: ct);
        }
    }
}