using System.Collections.Generic;

/// <summary>
/// EnemyAttackDatabaseSO를 조회하여 UnitId에 맞는 공격 패턴 목록을 확보하고,
/// 쿨타임 및 sqrMagnitude 사거리를 평가하여 공격 전략을 실행하는 순수 C# 전투 모듈
/// </summary>
public class EnemyCombatModule : UnitBaseModule
{
    private readonly UnitId _unitId;
    private EnemyAttackDatabaseSO _attackDatabase;
    private IEnemyAttackStrategy _attackStrategy;

    private readonly Dictionary<AnimStateType, float> _patternCooldownTimers = new Dictionary<AnimStateType, float>();
    private float _globalCooldownTimer = 0f;
    private EnemyAttackData _selectedPattern;
    private bool _hasSelectedPattern;
    private bool _isAttacking = false;

    public EnemyAttackData CurrentSelectedPattern => _selectedPattern;
    public bool HasSelectedPattern => _hasSelectedPattern;
    public bool IsOnGlobalCooldown => _globalCooldownTimer > 0f;
    public bool IsAttacking => _isAttacking;
    public float GlobalCooldownTimer => _globalCooldownTimer;

    public EnemyCombatModule(UnitId unitId, EnemyAttackDatabaseSO attackDatabase, IEnemyAttackStrategy strategy)
    {
        _unitId = unitId;
        _attackDatabase = attackDatabase;
        _attackStrategy = strategy ?? new MeleeAttackStrategy();
    }

    public void SetAttackDatabase(EnemyAttackDatabaseSO database)
    {
        _attackDatabase = database;
        _attackDatabase?.InitDatabase();
    }

    /// <summary>
    /// 공격 모션이 종료된 후 쿨타임
    /// </summary>
    /// <param name="deltaTime"></param>
    public void TickCooldown(float deltaTime)
    {

        if (_isAttacking) return;

        if (_globalCooldownTimer > 0f)
        {
            _globalCooldownTimer = UnityEngine.Mathf.Max(0f, _globalCooldownTimer - deltaTime);
        }

        List<AnimStateType> keys = new List<AnimStateType>(_patternCooldownTimers.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            AnimStateType key = keys[i];
            if (_patternCooldownTimers[key] > 0f)
            {
                _patternCooldownTimers[key] -= UnityEngine.Mathf.Max(0f, _patternCooldownTimers[key] - deltaTime);
            }
        }
    }

    /// <summary>
    /// 수평면 sqrMagnitude 기반으로 현재 사거리 내 실행 가능한 최적 공격 패턴 선별
    /// </summary>
    public bool TrySelectAttackPattern(in EnemyAttackContext context, UnityEngine.Transform target, out EnemyAttackData selectedData)
    {
        selectedData = default;
        if (IsOnGlobalCooldown || target == null || _attackDatabase == null) return false;

        if (!_attackDatabase.TryGetAllAttacksForUnit(_unitId, out Dictionary<AnimStateType, EnemyAttackData> unitAttacks)) return false;

        UnityEngine.Vector3 toTarget = target.position - context.OwnerTransform.position;
        toTarget.y = 0f;
        float sqrDistance = toTarget.sqrMagnitude;

        foreach (var pair in unitAttacks)
        {
            AnimStateType stateType = pair.Key;
            EnemyAttackData data = pair.Value;

            if (_patternCooldownTimers.TryGetValue(stateType, out float cooldownRemaining) && cooldownRemaining > 0f)
                continue;

            float sqrRange = data.AttackRange * data.AttackRange;

            if (sqrDistance <= sqrRange && _attackStrategy.CanExecute(in context, target, in data))
            {
                selectedData = data;
                _selectedPattern = data;
                _hasSelectedPattern = true;
                return true;
            }
        }

        return false;
    }

    public void StartAttack(in EnemyAttackContext context, UnityEngine.Transform target, in EnemyAttackData data)
    {
        _isAttacking = true;
        _selectedPattern = data;
        _hasSelectedPattern = true;
        _attackStrategy.Execute(in context, target, in data);
    }

    public void UpdateAttack(in EnemyAttackContext context, UnityEngine.Transform target, in EnemyAttackData data, float normalizedTime)
        => _attackStrategy.Update(in context, target, in data, normalizedTime);

    public void StopAttack(in EnemyAttackContext context, float globalCooldown)
    {
        if (_hasSelectedPattern)
        {
            _globalCooldownTimer = UnityEngine.Mathf.Max(0.5f, globalCooldown);

            _patternCooldownTimers[_selectedPattern.AttackAnimStateType] = _selectedPattern.AttackCooldown;
        }

        _attackStrategy.Stop(in context);
        _isAttacking = false;
        _hasSelectedPattern = false;
        _selectedPattern = default;
    }
    /// <summary>
    /// 등록된 공격 패턴들 중 가장 긴 최대 사거리를 산출 (추적 대치 범위 기준점으로 사용)
    /// </summary>
    public float GetMaxAttackRange()
    {
        if (_attackDatabase == null) return 2.0f;
        if (!_attackDatabase.TryGetAllAttacksForUnit(_unitId, out Dictionary<AnimStateType, EnemyAttackData> unitAttacks))
        {
            return 2.0f;
        }

        float maxRange = 1.6f;
        foreach (var pair in unitAttacks)
        {
            if (pair.Value.AttackRange > maxRange)
            {
                maxRange = pair.Value.AttackRange;
            }
        }
        return maxRange;
    }
}
