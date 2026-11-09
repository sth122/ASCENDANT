using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "Dragon_MeteorShower", menuName = "Unit/Skill/Dragon/MeteorSkill")]
public class DragonMeteorSkillSO : BossSkillDataSO
{
    [Header("메테오 사양")]
    [SerializeField] private int _meteorCount = 5;
    [SerializeField] private float _spawnInterval = 0.45f;
    [SerializeField] private float _randomSpreadRadius = 4.5f;

    public override BossSkill CreateSkill(BossBTController owner)
    {
        return new RuntimeMeteorSkill(owner, _info, _timelineSteps.ToArray(), this);
    }

    private class RuntimeMeteorSkill : BossSkill
    {
        private readonly ActionTimelineStep[] _steps;
        private readonly DragonMeteorSkillSO _data;

        public RuntimeMeteorSkill(BossBTController owner, in SkillInfo info, ActionTimelineStep[] steps, DragonMeteorSkillSO data)
            : base(owner, info)
        {
            _steps = steps;
            _data = data;
        }

        public override async UniTask<bool> ExecuteAsync(CancellationToken ct)
        {
            Owner.Blackboard.IsInvincible = true; // [핵심] 시전 중 완전 무적화

            ActionTimelineRunner.ExecutionContext ctx = new ActionTimelineRunner.ExecutionContext
            {
                AnimController = Owner.AnimController,
                Movement = Owner.Movement,
                Rigidbody = Owner.Rigidbody,
                OwnerTransform = Owner.transform,
                Target = Owner.CurrentTarget,
                GlobalSpeedMultiplier = 1.0f,
                OnOpenHitbox = Owner.CombatController.OpenHitbox,
                OnCloseHitbox = Owner.CombatController.CloseHitbox,
                OnCloseAllHitboxes = Owner.CombatController.CloseAllHitboxes,
                OnCustomEventTrigger = SpawnMeteorShowerAsync,
                BaseDamage = Info.BaseDamage,
                BasePoiseDamage = Info.BasePoiseDamage,
                IsParryable = false,
                ImpactType = AttackImpactType.Heavy,
                KnockbackForce = Info.KnockbackForce
            };

            bool result = false;
            try
            {
                result = await ActionTimelineRunner.ExecuteTimelineAsync(_steps, ctx, ct);
            }
            finally
            {
                Owner.Blackboard.IsInvincible = false; // 시전 종료 즉시 무적 해제
            }
            return result;
        }

        private async UniTask SpawnMeteorShowerAsync(CancellationToken ct)
        {
            Transform target = Owner.CurrentTarget;
            if (target == null) return;

            for (int i = 0; i < _data._meteorCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                Vector2 circle = UnityEngine.Random.insideUnitCircle * _data._randomSpreadRadius;
                Vector3 targetFloorPos = target.position + new Vector3(circle.x, 0f, circle.y);

                // 지면 높이 보정
                if (Physics.Raycast(targetFloorPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, LayerMask.GetMask("Ground", "Default")))
                {
                    targetFloorPos = hit.point;
                }

                // 하늘 낙하 지점 (상공 18m)
                Vector3 skyOrigin = targetFloorPos + Vector3.up * 18f + UnityEngine.Random.insideUnitSphere * 2f;

                // 메테오 투사체 스폰 및 발사
                if (ObjectPoolManager.Instance != null)
                {
                    GameObject meteor = ObjectPoolManager.Instance.Spawn(PoolType.MeteorProjectile, skyOrigin, Quaternion.LookRotation(targetFloorPos - skyOrigin));
                    if (meteor != null && meteor.TryGetComponent<MeteorProjectile>(out var comp))
                    {
                        comp.Launch(Owner.gameObject, targetFloorPos, Info.BaseDamage, Info.BasePoiseDamage);
                    }
                }

                await UniTask.Delay(TimeSpan.FromSeconds(_data._spawnInterval), cancellationToken: ct);
            }
        }
    }
}