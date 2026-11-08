using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyCombatController : MonoBehaviour
{
    [System.Serializable]
    public struct HitboxEntry
    {
        public HitboxSlot Slot;
        public WeaponSweepHitbox Hitbox;
    }

    [Header("Hitbox Socket Registry")]
    [SerializeField] protected List<HitboxEntry> _hitboxSockets = new List<HitboxEntry>();
    protected readonly Dictionary<HitboxSlot, WeaponSweepHitbox> _hitboxMap = new Dictionary<HitboxSlot, WeaponSweepHitbox>();

    [Header("Sockets")]
    [SerializeField] protected Transform _mouthSocket;
    [SerializeField] protected Transform _rightHandSocket;

    protected EnemyBTController _owner;
    protected Rigidbody _rigidbody;

    protected readonly Dictionary<AnimStateType, EnemyAttackData> _basicAttacks = new Dictionary<AnimStateType, EnemyAttackData>();
    protected readonly Dictionary<AnimStateType, float> _attackCooldowns = new Dictionary<AnimStateType, float>();
    protected readonly List<EnemyAttackData> _validCandidates = new List<EnemyAttackData>();
    protected float _globalAttackCooldownTime = 0f;

    private static readonly Collider[] _areaHitBuffer = new Collider[16];

    public Transform MouthSocket => _mouthSocket != null ? _mouthSocket : transform;
    public Transform RightHandSocket => _rightHandSocket != null ? _rightHandSocket : transform;
    public bool IsGlobalCooldownReady => Time.time >= _globalAttackCooldownTime;

    public virtual void Init(EnemyBTController owner)
    {
        _owner = owner;
        _rigidbody = GetComponent<Rigidbody>();
        BuildHitboxRegistry();
    }

    private void BuildHitboxRegistry()
    {
        _hitboxMap.Clear();
        for (int i = 0; i < _hitboxSockets.Count; i++)
        {
            if (_hitboxSockets[i].Hitbox != null && !_hitboxMap.ContainsKey(_hitboxSockets[i].Slot))
            {
                _hitboxMap.Add(_hitboxSockets[i].Slot, _hitboxSockets[i].Hitbox);
            }
        }
    }

    public void BindBasicAttacks(Dictionary<AnimStateType, EnemyAttackData> attacks)
    {
        _basicAttacks.Clear();
        _attackCooldowns.Clear();

        foreach (var kvp in attacks)
        {
            _basicAttacks[kvp.Key] = kvp.Value;
            _attackCooldowns[kvp.Key] = 0f;
        }
    }

    public bool HasAnyReadyAttack(Transform target)
    {
        if (target == null || _basicAttacks.Count == 0) return false;
        Vector3 diff = target.position - transform.position;
        diff.y = 0f;
        float distSqr = diff.sqrMagnitude;

        foreach (var kvp in _basicAttacks)
        {
            if (distSqr <= (kvp.Value.AttackRange * kvp.Value.AttackRange) && Time.time >= _attackCooldowns[kvp.Key])
                return true;
        }
        return false;
    }

    public bool IsTargetWithinAnyAttackRange(Transform target)
    {
        if (target == null || _basicAttacks.Count == 0) return false;
        Vector3 diff = target.position - transform.position;
        diff.y = 0f;
        float distSqr = diff.sqrMagnitude;

        foreach (var kvp in _basicAttacks)
        {
            if (distSqr <= (kvp.Value.AttackRange * kvp.Value.AttackRange)) return true;
        }
        return false;
    }

    public bool TryPickValidAttack(Transform target, out EnemyAttackData chosenData)
    {
        _validCandidates.Clear();
        Vector3 diff = target.position - transform.position;
        diff.y = 0f;
        float distSqr = diff.sqrMagnitude;

        foreach (var kvp in _basicAttacks)
        {
            if (distSqr <= (kvp.Value.AttackRange * kvp.Value.AttackRange) && Time.time >= _attackCooldowns[kvp.Key])
                _validCandidates.Add(kvp.Value);
        }

        if (_validCandidates.Count == 0)
        {
            chosenData = null;
            return false;
        }

        chosenData = _validCandidates[UnityEngine.Random.Range(0, _validCandidates.Count)];
        return true;
    }

    /// <summary>
    /// 공격 시작 즉시 글로벌 쿨다운을 예약하여 프레임 레이스 컨디션을 차단
    /// </summary>
    public void ReserveGlobalCooldown(float expectedCooldown)
    {
        _globalAttackCooldownTime = Time.time + Mathf.Max(1.5f, expectedCooldown * 0.5f);
    }

    public void RecordAttackCooldown(AnimStateType animState, float cooldown)
    {
        _attackCooldowns[animState] = Time.time + cooldown;
        _globalAttackCooldownTime = Time.time + 1.2f;
    }

    public async UniTask<bool> ExecuteAttackTimelineAsync(EnemyAttackData attackData, Transform target,
        float baseAtk, float basePoise, float speedMod, System.Func<CancellationToken, UniTask> customEvent, CancellationToken ct)
    {
        ActionTimelineStep[] steps = attackData.GetTimelineSteps();

        ActionTimelineRunner.ExecutionContext ctx = new ActionTimelineRunner.ExecutionContext
        {
            AnimController = _owner.AnimController,
            Movement = _owner.Movement,
            Rigidbody = _rigidbody,
            OwnerTransform = transform,
            Target = target,
            GlobalSpeedMultiplier = speedMod,
            OnOpenHitbox = OpenHitbox,
            OnCloseHitbox = CloseHitbox,
            OnCloseAllHitboxes = CloseAllHitboxes,
            OnCustomEventTrigger = customEvent,
            BaseDamage = attackData.CalculateFinalDamage(baseAtk),
            BasePoiseDamage = attackData.CalculateFinalPoiseDamage(basePoise),
            IsParryable = attackData.IsParryable,
            ImpactType = attackData.ImpactType,
            KnockbackForce = attackData.KnockbackForce
        };

        return await ActionTimelineRunner.ExecuteTimelineAsync(steps, ctx, ct);
    }

    public void OpenHitbox(HitboxSlot slot, float damage, float poiseDamage, bool isParryable, AttackImpactType impactType, float knockback)
    {
        if (slot == HitboxSlot.BothHands)
        {
            OpenSingle(HitboxSlot.RightHand, damage, poiseDamage, isParryable, impactType, knockback);
            OpenSingle(HitboxSlot.LeftHand, damage, poiseDamage, isParryable, impactType, knockback);
            return;
        }
        OpenSingle(slot, damage, poiseDamage, isParryable, impactType, knockback);
    }

    private void OpenSingle(HitboxSlot slot, float damage, float poiseDamage, bool isParryable, AttackImpactType impactType, float knockback)
    {
        if (_hitboxMap.TryGetValue(slot, out var hitbox) && hitbox != null)
        {
            hitbox.OpenHitBox(gameObject, damage, poiseDamage, isParryable, impactType, knockback);
        }
    }

    public void CloseHitbox(HitboxSlot slot)
    {
        if (slot == HitboxSlot.BothHands)
        {
            CloseSingle(HitboxSlot.RightHand);
            CloseSingle(HitboxSlot.LeftHand);
            return;
        }
        CloseSingle(slot);
    }

    private void CloseSingle(HitboxSlot slot)
    {
        if (_hitboxMap.TryGetValue(slot, out var hitbox) && hitbox != null)
        {
            hitbox.CloseHitBox();
        }
    }

    public void CloseAllHitboxes()
    {
        foreach (var hb in _hitboxMap.Values)
        {
            hb?.CloseHitBox();
        }
    }

    public void ExecuteAreaDamageNonAlloc(Vector3 center, float radius, float maxHeight, float damage, float poiseDamage,
        AttackImpactType impactType, float knockback, LayerMask targetLayer, LayerMask obstacleLayer)
    {
        int count = Physics.OverlapSphereNonAlloc(center, radius, _areaHitBuffer, targetLayer);

        for (int i = 0; i < count; i++)
        {
            Collider col = _areaHitBuffer[i];
            if (col == null || col.transform.root == transform.root) continue;

            if (Mathf.Abs(col.transform.position.y - center.y) > maxHeight) continue;

            Vector3 rayOrigin = center + Vector3.up * 0.3f;
            Vector3 targetHead = col.bounds.center;
            if (Physics.Linecast(rayOrigin, targetHead, obstacleLayer)) continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                DamageContext ctx = new DamageContext(gameObject, damage, poiseDamage, col.transform.position, Vector3.up, false, impactType, knockback);
                damageable.ApplyDamage(in ctx);
            }
        }
        System.Array.Clear(_areaHitBuffer, 0, count);
    }
}