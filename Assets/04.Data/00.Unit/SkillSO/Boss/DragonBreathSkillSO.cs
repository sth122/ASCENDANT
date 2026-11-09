using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "DragonBreathSkill", menuName = "Unit/Skill/Dragon/BreathSkill")]
public class DragonBreathSkillSO : BossSkillDataSO
{
    public enum BreathType { Linear, SweepFan }

    [Header("브레스 형태 및 판정")]
    [SerializeField] private BreathType _breathType = BreathType.Linear;
    [SerializeField] private float _breathLength = 15.0f;
    [SerializeField] private float _breathAngle = 60.0f;
    [SerializeField] private float _breathDuration = 2.4f; // 스킬별 지속 방출 시간
    [SerializeField] private float _tickInterval = 0.25f; // 0.25초마다 틱딜
    [SerializeField] private LayerMask _targetLayer;
    public float BreathDuration => _breathDuration;

    public override BossSkill CreateSkill(BossBTController owner)
    {
        return new RuntimeBreathSkill(owner, _info, _timelineSteps.ToArray(), this);
    }

    private class RuntimeBreathSkill : BossSkill
    {
        private readonly ActionTimelineStep[] _steps;
        private readonly DragonBreathSkillSO _data;
        private CancellationTokenSource _breathCts;

        public RuntimeBreathSkill(BossBTController owner, in SkillInfo info, ActionTimelineStep[] steps, DragonBreathSkillSO data)
            : base(owner, info)
        {
            _steps = steps;
            _data = data;
        }

        public override async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            float speedMod = Owner.Blackboard.IsPhase2 ? Owner.Blackboard.Phase2StatBuffMultiplier : 1.0f;

            ActionTimelineRunner.ExecutionContext ctx = new ActionTimelineRunner.ExecutionContext
            {
                AnimController = Owner.AnimController,
                Movement = Owner.Movement,
                Rigidbody = Owner.Rigidbody,
                OwnerTransform = Owner.transform,
                Target = Owner.CurrentTarget,
                GlobalSpeedMultiplier = speedMod,
                OnOpenHitbox = Owner.OpenHitboxSlot,
                OnCloseHitbox = Owner.CloseHitboxSlot,
                OnCloseAllHitboxes = Owner.CloseAllHitboxes,
                OnCustomEventTrigger = async (token) =>
                {
                    _breathCts?.Cancel();
                    _breathCts?.Dispose();
                    _breathCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    RunBreathRoutineAsync(_breathCts.Token).Forget();
                    await UniTask.CompletedTask;
                },
                BaseDamage = Info.BaseDamage,
                BasePoiseDamage = Info.BasePoiseDamage,
                IsParryable = Info.IsParryable,
                ImpactType = Info.ImpactType,
                KnockbackForce = Info.KnockbackForce
            };

            bool result = false;
            try
            {
                result = await ActionTimelineRunner.ExecuteTimelineAsync(_steps, ctx, ct);
            }
            finally
            {
                _breathCts?.Cancel();
                _breathCts?.Dispose();
                _breathCts = null;
            }
            return result;
        }

        private async UniTaskVoid RunBreathRoutineAsync(CancellationToken token)
        {
            Transform mouth = Owner.CombatController.MouthSocket;
            GameObject vfxObj = null;

            // 드래곤의 전방 회전값 산출 (Quaternion.identity 완전 제거)
            Quaternion spawnRot = (_data._breathType == BreathType.SweepFan)
                ? mouth.rotation
                : Quaternion.LookRotation(Owner.transform.forward);

            if (ObjectPoolManager.Instance != null)
            {
                vfxObj = ObjectPoolManager.Instance.Spawn(PoolType.VFX_Breath_Fire, mouth.position, spawnRot, mouth);
                if (vfxObj != null)
                {
                    vfxObj.transform.localScale = Vector3.one;
                }
            }

            float duration = _data._breathDuration;
            float elapsed = 0f;
            Collider[] hitBuffer = new Collider[16];

            try
            {
                while (elapsed < duration && !token.IsCancellationRequested)
                {
                    Vector3 origin = mouth.position;
                    Vector3 forward = (_data._breathType == BreathType.SweepFan) ? mouth.forward : Owner.transform.forward;

                    if (_data._breathType == BreathType.Linear)
                    {
                        Vector3 center = origin + forward * (_data._breathLength * 0.5f);
                        Vector3 halfExtents = new Vector3(2.0f, 2.5f, _data._breathLength * 0.5f);
                        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, hitBuffer, Quaternion.LookRotation(forward), _data._targetLayer);
                        ApplyDamage(hitBuffer, count);
                    }
                    else
                    {
                        int count = Physics.OverlapSphereNonAlloc(origin, _data._breathLength, hitBuffer, _data._targetLayer);
                        for (int i = 0; i < count; i++)
                        {
                            if (hitBuffer[i] == null || hitBuffer[i].transform.root == Owner.transform.root) continue;
                            Vector3 dir = (hitBuffer[i].transform.position - origin).normalized;
                            dir.y = 0f;
                            if (Vector3.Angle(forward, dir) <= (_data._breathAngle * 0.5f))
                            {
                                var damageable = hitBuffer[i].GetComponentInParent<IDamageable>();
                                damageable?.ApplyDamage(new DamageContext(Owner.gameObject, Info.BaseDamage * 0.35f, Info.BasePoiseDamage * 0.2f, hitBuffer[i].transform.position, Vector3.up, false, AttackImpactType.DoT));
                            }
                        }
                    }

                    elapsed += _data._tickInterval;
                    await UniTask.Delay(TimeSpan.FromSeconds(_data._tickInterval), cancellationToken: token);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (vfxObj != null && ObjectPoolManager.Instance != null)
                {
                    vfxObj.transform.SetParent(null);
                    ObjectPoolManager.Instance.Despawn(PoolType.VFX_Breath_Fire, vfxObj);
                }
            }
        }

        private void ApplyDamage(Collider[] buffer, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (buffer[i] == null || buffer[i].transform.root == Owner.transform.root) continue;
                var damageable = buffer[i].GetComponentInParent<IDamageable>();
                damageable?.ApplyDamage(new DamageContext(Owner.gameObject, Info.BaseDamage * 0.35f, Info.BasePoiseDamage * 0.2f, buffer[i].transform.position, Vector3.up, false, AttackImpactType.DoT));
            }
        }
    }
}