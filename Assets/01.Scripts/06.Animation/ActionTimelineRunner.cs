using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class ActionTimelineRunner
{
    private readonly struct BoundVfxEntry
    {
        public readonly PoolType VfxType;
        public readonly GameObject Instance;

        public BoundVfxEntry(PoolType vfxType, GameObject instance)
        {
            VfxType = vfxType;
            Instance = instance;
        }
    }

    public struct ExecutionContext
    {
        public UnitAnimController AnimController;
        public EnemyMovement Movement;
        public Rigidbody Rigidbody;
        public Transform OwnerTransform;
        public Transform Target;
        public float GlobalSpeedMultiplier;

        public Action<HitboxSlot, float, float, bool, AttackImpactType, float> OnOpenHitbox;
        public Action<HitboxSlot> OnCloseHitbox;
        public Action OnCloseAllHitboxes;

        public Func<HitboxSlot, Transform> ResolveSocket;
        public Func<CancellationToken, UniTask> OnCustomEventTrigger;

        public float BaseDamage;
        public float BasePoiseDamage;
        public bool IsParryable;
        public AttackImpactType ImpactType;
        public float KnockbackForce;
    }

    public static async UniTask<bool> ExecuteTimelineAsync(ActionTimelineStep[] steps, ExecutionContext ctx, CancellationToken ct)
    {
        if (steps == null || steps.Length == 0) return false;

        bool wasAirborne = false;
        try
        {
            for (int i = 0; i < steps.Length; i++)
            {
                if (ct.IsCancellationRequested) return false;
                wasAirborne = steps[i].IsAirborne;
                await ExecuteSingleStepAsync(steps[i], ctx, ct);
            }
        }
        finally
        {
            ctx.OnCloseAllHitboxes?.Invoke();
            ctx.AnimController?.SetRootMotion(false);
            ctx.Movement?.EndRootMotionMovement();

            if (ctx.Rigidbody != null && !ctx.Rigidbody.isKinematic)
            {
                ctx.Rigidbody.linearVelocity = Vector3.zero;
                ctx.Rigidbody.angularVelocity = Vector3.zero;
            }

            if (wasAirborne && ctx.OwnerTransform != null)
            {
                Vector3 currentPos = ctx.OwnerTransform.position;
                if (Physics.Raycast(currentPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 50f, LayerMask.GetMask("Ground", "Default")))
                {
                    ctx.OwnerTransform.position = hit.point;
                    if (ctx.Movement != null && ctx.Movement.Agent != null && ctx.Movement.Agent.isOnNavMesh)
                    {
                        ctx.Movement.Agent.Warp(hit.point);
                    }
                }
            }
        }

        return true;
    }

    private static async UniTask ExecuteSingleStepAsync(ActionTimelineStep step, ExecutionContext ctx, CancellationToken ct)
    {
        if (step.FaceTargetOnEnter && ctx.Target != null && ctx.Movement != null)
        {
            ctx.Movement.FaceTarget(ctx.Target.position, 12.0f, Time.deltaTime);
        }

        if (step.UseRootMotion && ctx.Movement != null)
        {
            ctx.Movement.BeginRootMotionMovement();
            ctx.AnimController.SetRootMotion(true, (deltaPos, deltaRot) =>
            {
                ctx.Movement.ApplyRootMotionDelta(deltaPos, deltaRot, step.IsAirborne);
            });
        }
        else
        {
            ctx.AnimController.SetRootMotion(false);
        }

        ctx.AnimController.PlayState(step.StateType, force: true);

        float clipDuration = ctx.AnimController.GetStateDuration(step.StateType);

        float stepSpeed = Mathf.Max(0.1f, (step.SpeedMultiplier > 0 ? step.SpeedMultiplier : 1.0f) * ctx.GlobalSpeedMultiplier);
        float totalDuration = clipDuration / stepSpeed;

        float elapsed = 0f;
        bool hitboxOpened = false;
        bool eventFired = false;
        bool dashActive = false;
        Vector3 dashDirection = ctx.OwnerTransform != null ? ctx.OwnerTransform.forward : Vector3.forward;

        int cueCount = step.CueEvents != null ? step.CueEvents.Length : 0;
        bool[] cueTriggered = cueCount > 0 ? new bool[cueCount] : null;
        List<BoundVfxEntry> stepBoundVfx = cueCount > 0 ? new List<BoundVfxEntry>(cueCount) : null;

        try
        {
            while (elapsed < totalDuration)
            {
                ct.ThrowIfCancellationRequested();

                float norm = Mathf.Clamp01(elapsed / totalDuration);

                // 1. 다중 VFX / SFX 연출 큐 발동
                for (int c = 0; c < cueCount; c++)
                {
                    if (cueTriggered[c]) continue;
                    TimelineCueEvent cue = step.CueEvents[c];
                    if (norm < cue.TriggerNormalized) continue;

                    cueTriggered[c] = true;
                    Transform socket = ctx.ResolveSocket != null ? ctx.ResolveSocket(cue.SpawnSocket) : ctx.OwnerTransform;
                    if (socket == null) socket = ctx.OwnerTransform;

                    Vector3 worldPos = socket != null ? socket.TransformPoint(cue.LocalOffset) : Vector3.zero;
                    Quaternion worldRot = socket != null ? socket.rotation : Quaternion.identity;

                    if (cue.VfxType != PoolType.None && VFXManager.Instance != null)
                    {
                        Transform parent = cue.AttachToSocket ? socket : null;
                        GameObject vfx = VFXManager.Instance.PlayVFX(cue.VfxType, worldPos, worldRot, parent);
                        if (vfx != null && cue.DespawnOnStepExit)
                        {
                            stepBoundVfx.Add(new BoundVfxEntry(cue.VfxType, vfx));
                        }
                    }

                    if (cue.SfxId != SoundId.None && SoundManager.Instance != null)
                    {
                        float vol = cue.VolumeMultiplier > 0f ? cue.VolumeMultiplier : 1.0f;
                        SoundManager.Instance.PlaySFX(cue.SfxId, worldPos, vol);
                    }
                }

                // 2. 특수 비동기 이벤트 발동
                if (step.HasCustomEvent && !eventFired && norm >= step.EventTriggerNormalized)
                {
                    eventFired = true;
                    if (ctx.OnCustomEventTrigger != null)
                    {
                        ctx.OnCustomEventTrigger.Invoke(ct).Forget();
                    }
                }

                // 3. 히트박스 오픈 / 클로즈
                if (step.HasHitbox)
                {
                    if (!hitboxOpened && norm >= step.HitboxStartNormalized && norm < step.HitboxEndNormalized)
                    {
                        hitboxOpened = true;
                        ctx.OnOpenHitbox?.Invoke(step.TargetHitboxSlot, ctx.BaseDamage, ctx.BasePoiseDamage,
                            ctx.IsParryable, ctx.ImpactType, ctx.KnockbackForce);
                    }
                    else if (hitboxOpened && norm >= step.HitboxEndNormalized)
                    {
                        hitboxOpened = false;
                        ctx.OnCloseAllHitboxes?.Invoke();
                    }
                }

                // 4. 물리 돌진
                if (step.EnablePhysicalDash)
                {
                    if (!dashActive && norm >= step.DashStartNormalized && norm < step.DashEndNormalized)
                    {
                        dashActive = true;
                        if (ctx.Target != null && ctx.OwnerTransform != null)
                        {
                            dashDirection = (ctx.Target.position - ctx.OwnerTransform.position).normalized;
                            dashDirection.y = 0f;
                            ctx.OwnerTransform.rotation = Quaternion.LookRotation(dashDirection);
                        }
                    }
                    else if (dashActive && norm >= step.DashEndNormalized)
                    {
                        dashActive = false;
                        if (ctx.Rigidbody != null && !ctx.Rigidbody.isKinematic)
                        {
                            ctx.Rigidbody.linearVelocity = Vector3.zero;
                        }
                    }

                    if (dashActive && ctx.Rigidbody != null && !ctx.Rigidbody.isKinematic)
                    {
                        ctx.Rigidbody.linearVelocity = dashDirection * step.DashSpeed;
                    }
                }

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
        finally
        {
            if (hitboxOpened) ctx.OnCloseAllHitboxes?.Invoke();

            if (stepBoundVfx != null && VFXManager.Instance != null)
            {
                for (int i = 0; i < stepBoundVfx.Count; i++)
                {
                    BoundVfxEntry entry = stepBoundVfx[i];
                    VFXManager.Instance.DespawnVFX(entry.VfxType, entry.Instance);
                }
            }
        }
    }
}