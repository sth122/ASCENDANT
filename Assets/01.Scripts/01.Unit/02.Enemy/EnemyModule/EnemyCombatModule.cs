using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyAttackDatabaseSO를 조회하여 UnitId에 맞는 공격 패턴 목록을 확보하고,
/// 쿨타임 및 sqrMagnitude 사거리를 평가하여 공격 전략을 실행하는 순수 C# 전투 모듈
/// </summary>
public class EnemyCombatModule : UnitBaseModule
{
    private readonly EnemyController _owner;
    private readonly UnitId _unitId;
    private EnemyAttackDatabaseSO _attackDatabase;
    private IEnemyAttackStrategy _attackStrategy;

    private float _attackCooldownTimer;
    private EnemyAttackData _selectedPattern;
    private bool _hasSelectedPattern;

    public EnemyAttackData CurrentSelectedPattern => _selectedPattern;
    public bool HasSelectedPattern => _hasSelectedPattern;
    public bool IsOnCooldown => _attackCooldownTimer > 0f;

    public EnemyCombatModule(EnemyController owner, UnitId unitId, EnemyAttackDatabaseSO attackDatabase, IEnemyAttackStrategy strategy)
    {
        _owner = owner;
        _unitId = unitId;
        _attackDatabase = attackDatabase;
        _attackStrategy = strategy ?? new MeleeAttackStrategy();
    }

    public void SetAttackDatabase(EnemyAttackDatabaseSO database)
    {
        _attackDatabase = database;
        if (_attackDatabase != null)
        {
            _attackDatabase.InitDatabase();
        }
    }

    public void SetAttackStrategy(IEnemyAttackStrategy newStrategy)
    {
        _attackStrategy = newStrategy;
    }

    public void TickCooldown(float deltaTime)
    {
        if (_attackCooldownTimer > 0f)
        {
            _attackCooldownTimer -= deltaTime;
        }
    }

    /// <summary>
    /// 수평면 sqrMagnitude 기반으로 현재 사거리 내 실행 가능한 최적 공격 패턴 선별
    /// </summary>
    public bool TrySelectAttackPattern(Transform target, out EnemyAttackData selectedData)
    {
        selectedData = default;
        if (IsOnCooldown || target == null || _attackDatabase == null) return false;

        if (!_attackDatabase.TryGetAllAttacksForUnit(_unitId, out Dictionary<AnimStateType, EnemyAttackData> unitAttacks))
        {
            return false;
        }

        Vector3 toTarget = target.position - _owner.transform.position;
        toTarget.y = 0f;
        float sqrDistance = toTarget.sqrMagnitude;

        EnemyAttackContext context = _owner.CreateAttackContext();

        foreach (var pair in unitAttacks)
        {
            EnemyAttackData data = pair.Value;
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

    public void StartAttack(Transform target, in EnemyAttackData data)
    {
        _selectedPattern = data;
        _hasSelectedPattern = true;
        EnemyAttackContext context = _owner.CreateAttackContext();
        _attackStrategy.Execute(in context, target, in data);
    }

    public void StopAttack()
    {
        if (_hasSelectedPattern)
        {
            _attackCooldownTimer = _selectedPattern.AttackCooldown;
        }

        EnemyAttackContext context = _owner.CreateAttackContext();
        _attackStrategy.Stop(in context);
        _hasSelectedPattern = false;
        _selectedPattern = default;
    }
}
