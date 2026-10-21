using Cysharp.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// EnemyAttackDatabaseSO를 조회하여 UnitId에 맞는 공격 패턴 목록을 확보하고,
/// 쿨타임 및 sqrMagnitude 사거리를 평가하여 공격 전략을 실행하는 순수 C# 전투 모듈
/// </summary>
public class EnemyCombatModule : UnitBaseModule, System.IDisposable
{
    private readonly UnitId _unitId;
    private EnemyAttackDatabaseSO _attackDatabase;
    private IEnemyAttackStrategy _attackStrategy;

    private readonly Dictionary<AnimStateType, float> _patternCooldownTimers = new Dictionary<AnimStateType, float>();
    private readonly HashSet<AnimStateType> _activeCooldownPatterns = new HashSet<AnimStateType>();

    private readonly List<EnemyAttackData> _validAttackCandidates = new List<EnemyAttackData>(8);

    private float _globalCooldownTimer = 0f;
    private bool _isGlobalCooldownActive = false;
    private EnemyAttackData _selectedPattern;
    private bool _hasSelectedPattern = false;
    private bool _isAttacking = false;

    private System.Threading.CancellationTokenSource _combatCts;

    public EnemyAttackData CurrentSelectedPattern => _selectedPattern;
    public bool HasSelectedPattern => _hasSelectedPattern;
    public bool IsOnGlobalCooldown => _isGlobalCooldownActive;
    public bool IsAttacking => _isAttacking;

    public EnemyCombatModule(UnitId unitId, EnemyAttackDatabaseSO attackDatabase, IEnemyAttackStrategy strategy)
    {
        _unitId = unitId;
        _attackDatabase = attackDatabase;
        _attackStrategy = strategy ?? new MeleeAttackStrategy();
        _combatCts = new System.Threading.CancellationTokenSource();
    }

    public void SetAttackDatabase(EnemyAttackDatabaseSO database)
    {
        _attackDatabase = database;
        _attackDatabase?.InitDatabase();

        if (_attackDatabase != null && _attackDatabase.TryGetAllAttacksForUnit(_unitId, out var unitAttacks))
        {
            _patternCooldownTimers.Clear();
            foreach (var key in unitAttacks.Keys)
            {
                _patternCooldownTimers[key] = 0f;
            }
        }
    }

    /// <summary>
    /// 수평면 sqrMagnitude 기반으로 현재 사거리 내에 쿨타임 조건을 만족하는 공격 패턴 랜덤 선별
    /// </summary>
    public bool TrySelectAttackPattern(in EnemyAttackContext context, UnityEngine.Transform target, out EnemyAttackData selectedData)
    {
        selectedData = default;

        if (IsOnGlobalCooldown || target == null || _attackDatabase == null) return false;

        if (!_attackDatabase.TryGetAllAttacksForUnit(_unitId, out Dictionary<AnimStateType, EnemyAttackData> unitAttacks)) return false;

        UnityEngine.Vector3 toTarget = target.position - context.OwnerTransform.position;
        toTarget.y = 0f;
        float sqrDistance = toTarget.sqrMagnitude;

        _validAttackCandidates.Clear();

        // 1. 실행 가능한 모든 공격 패턴 수집
        foreach (var pair in unitAttacks)
        {
            AnimStateType stateType = pair.Key;
            EnemyAttackData data = pair.Value;

            if (_activeCooldownPatterns.Contains(stateType))
                continue;

            float sqrRange = data.AttackRange * data.AttackRange;

            if (sqrDistance <= sqrRange && _attackStrategy.CanExecute(in context, target, in data))
            {
                _validAttackCandidates.Add(data);
            }
        }
        // 2. 후보가 존재하면 무작위(Random) 1종 선택
        if (_validAttackCandidates.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, _validAttackCandidates.Count);
            selectedData = _validAttackCandidates[randomIndex];
            _selectedPattern = selectedData;
            _hasSelectedPattern = true;
            return true;
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
            AnimStateType executedPattern = _selectedPattern.AttackAnimStateType;
            float patternCooldown = _selectedPattern.AttackCooldown;

            // 1. 해당 패턴 비동기 쿨타임 루틴 기동
            StartPatternCooldownAsync(executedPattern, patternCooldown, _combatCts.Token).Forget();

            // 2. 글로벌 쿨타임 비동기 루틴 기동
            StartGlobalCooldownAsync(globalCooldown, _combatCts.Token).Forget();
        }

        _attackStrategy.Stop(in context);
        _isAttacking = false;
        _hasSelectedPattern = false;
        _selectedPattern = default;
    }

    private async UniTaskVoid StartPatternCooldownAsync(AnimStateType pattern, float duration, System.Threading.CancellationToken token)
    {
        _activeCooldownPatterns.Add(pattern);
        _patternCooldownTimers[pattern] = duration;

        float remaining = duration;
        while (remaining > 0f)
        {
            bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (canceled) return;

            remaining -= UnityEngine.Time.deltaTime;
            _patternCooldownTimers[pattern] = UnityEngine.Mathf.Max(0f, remaining);
        }

        _activeCooldownPatterns.Remove(pattern);
        _patternCooldownTimers[pattern] = 0f;
    }

    private async UniTaskVoid StartGlobalCooldownAsync(float duration, System.Threading.CancellationToken token)
    {
        _isGlobalCooldownActive = true;
        _globalCooldownTimer = duration;

        float remaining = duration;
        while (remaining > 0f)
        {
            bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (canceled) return;

            remaining -= UnityEngine.Time.deltaTime;
            _globalCooldownTimer = UnityEngine.Mathf.Max(0f, remaining);
        }

        _isGlobalCooldownActive = false;
        _globalCooldownTimer = 0f;
    }

    /// <summary>
    /// 등록된 공격 패턴들 중 가장 긴 최대 사거리를 산출 (추적 대치 범위 기준점으로 사용)
    /// </summary>
    public float GetMaxAttackRange()
    {
        if (_attackDatabase == null) return 2.0f;
        if (!_attackDatabase.TryGetAllAttacksForUnit(_unitId, out Dictionary<AnimStateType, EnemyAttackData> unitAttacks))
            return 2.0f;

        float maxRange = 1.6f;
        foreach (var pair in unitAttacks)
        {
            if (pair.Value.AttackRange > maxRange){
                maxRange = pair.Value.AttackRange;
            }
        }
        return maxRange;
    }

    public void Dispose()
    {
        AsyncUtil.CancelAndDispose(ref _combatCts);
        _activeCooldownPatterns.Clear();
        _validAttackCandidates.Clear();
    }
}
