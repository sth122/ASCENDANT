using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Util = DebugLogger;

/// <summary>
/// 모든 보스가 공유하는 최상위 BT 컨트롤러
/// 블랙보드 기반 주기적 스킬 발동, 컷신 조우 시퀀스, 페이즈 전환 트리거를 총괄합니다.
/// </summary>
public abstract class BossBTController : EnemyBTController, ISkillable
{
    [Header("Boss Blackboard")]
    [SerializeField] protected BossBlackboard _blackboard = new BossBlackboard();

    protected readonly Dictionary<SkillId, BossSkill> _skillMap = new Dictionary<SkillId, BossSkill>();
    private readonly List<SkillId> _cachedReadySkills = new List<SkillId>(8);

    public BossBlackboard Blackboard => _blackboard;

    protected override void SetupComponents()
    {
        _groggyRecoveryDuration = 20.0f;
        base.SetupComponents();
        SetControllerActive(false);
    }

    public override void Init()
    {
        base.Init();
        EnsureDatabaseAndSkillsAsync(this.destroyCancellationToken).Forget();
    }

    public override void BindStatusData(EnemyInfo overrideStat = null)
    {
        BossInfo runtimeBossInfo = null;

        if (overrideStat is BossInfo bossStat)
        {
            runtimeBossInfo = bossStat;
        }
        else if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<BossInfoSO>(out var bossInfoSO))
        {
            if (bossInfoSO.TryGetData(_unitId, out BossInfo baseStatus))
            {
                runtimeBossInfo = (BossInfo)baseStatus.Clone();
            }
        }

        if (runtimeBossInfo != null)
        {
            _enemyStatus.Init(runtimeBossInfo);

            _blackboard.Phase2HpRatio = runtimeBossInfo.Phase2ThresholdRatio;
            _blackboard.Phase2HealRatio = runtimeBossInfo.Phase2HealRatio;
            _blackboard.Phase2StatBuffMultiplier = runtimeBossInfo.Phase2StatBuffMultiplier;
            _blackboard.SkillCycleInterval = runtimeBossInfo.SkillCycleInterval;
            _blackboard.SkillCooldownResetChance = runtimeBossInfo.SkillCooldownResetChance;

            _blackboard.ResetSkillCycle();
        }
    }

    private async UniTaskVoid EnsureDatabaseAndSkillsAsync(CancellationToken ct)
    {
        try
        {
            if (DataManager.Instance != null && !DataManager.Instance.IsDataLoaded)
            {
                await DataManager.Instance.EnsureDataLoadedAsync(ct);
            }

            BindStatusData();
            BindCombatDatabase();
            RegisterSkillsFromDatabase();
            _blackboard.ResetSkillCycle();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Util.LogErrorWithTag(name, $"보스 DB 및 스킬 바인딩 실패: {ex.Message}");
        }
    }

    public void RegisterSkillsFromDatabase()
    {
        _skillMap.Clear();

        if (DataManager.Instance != null && DataManager.Instance.TryGetSingle<UnitCombatDatabaseSO>(out var combatDb))
        {
            if (combatDb.TryGetSkills(_unitId, out var skillDataList))
            {
                for (int i = 0; i < skillDataList.Count; i++)
                {
                    BossSkillDataSO dataSO = skillDataList[i];
                    if (dataSO != null)
                    {
                        BossSkill skill = dataSO.CreateSkill(this);
                        _skillMap[dataSO.Info.Id] = skill;
                    }
                }
            }
        }
    }

    public override void OnParried(float parryPoiseDamage = 35.0f)
    {
        if (IsDead) return;
        _enemyStatus.TakeDamage(0f, 10.0f);
        Util.LogWithTag(name, "[보스 패링 피격] 경직 면역: 패턴 유지");
    }

    public virtual void TriggerEncounterSequence(Transform playerTransform) =>
        ExecuteEncounterAsync(playerTransform, GetActionToken()).Forget();

    private async UniTaskVoid ExecuteEncounterAsync(Transform playerTransform, CancellationToken token)
    {
        Util.LogWithTag(name, "[보스 조우 시퀀스 개시]");
        _blackboard.IsInvincible = true;

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.PlayBossCutscene(transform);
        }

        transform.position = _patrolModule.SpawnPoint;
        _movement.StopMovement();
        SetTarget(playerTransform);

        SetBossEncounterEvent();

        try
        {
            await PlayStateAndWaitAsync(AnimStateType.Roar, token, force: true);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _blackboard.IsInvincible = false;

            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.ResetToFreeLook();
            }

            SetControllerActive(true);
            _blackboard.ResetSkillCycle();
        }
    }

    protected void SetBossEncounterEvent()
    {
        string bossName = _unitId.ToString();
        float curHp = _enemyStatus != null ? _enemyStatus.CurrentHp : 1000f;
        float maxHp = _enemyStatus != null ? _enemyStatus.MaxHp : 1000f;
        float curPoise = _enemyStatus != null ? _enemyStatus.CurrentPoise : 200f;
        float maxPoise = _enemyStatus != null ? _enemyStatus.MaxPoise : 200f;

        EventBusSystem.Publish(new BossEncounterEvent(bossName, gameObject, curHp, maxHp, curPoise, maxPoise));
    }

    #region ISkillable 구현부
    public bool CanExecuteSkill(SkillId skillId) => CanExecuteSkill(skillId, _currentTarget);

    public bool CanExecuteSkill(SkillId skillId, Transform target)
    {
        return _skillMap.TryGetValue(skillId, out BossSkill skill) &&
               (target != null ? skill.CanExecute(target) : skill.CanExecute());
    }

    public async UniTask<bool> ExecuteSkillAsync(SkillId skillId, CancellationToken ct)
    {
        if (_skillMap.TryGetValue(skillId, out BossSkill skill))
        {
            SetActionState(EnemyActionState.SkillCasting);
            _blackboard.IsSkillPending = false;
            _movement.StopMovement();

            bool success = false;
            try
            {
                success = await skill.ExecuteAsync(ct);
            }
            finally
            {
                if (_blackboard.IsPhase2 && UnityEngine.Random.value <= _blackboard.SkillCooldownResetChance)
                {
                    skill.ResetCooldown();
                    _blackboard.ForceReadySkillCycle();
                }
                else
                {
                    skill.TriggerCooldown();
                    _blackboard.ResetSkillCycle();
                }

                if (ActionState == EnemyActionState.SkillCasting)
                {
                    SetActionState(EnemyActionState.None);
                }
            }

            return success;
        }
        return false;
    }

    public void CancelSkill()
    {
        _combatController.CloseAllHitboxes();
        SetActionState(EnemyActionState.None);
        AsyncUtil.CancelAndDispose(ref _actionCts);
    }

    public bool HasAnyReadySkill(Transform target)
    {
        foreach (var pair in _skillMap)
        {
            if (pair.Value.CanExecute(target)) return true;
        }
        return false;
    }

    public bool HasAnyReadySkill() => HasAnyReadySkill(_currentTarget);

    protected async UniTask<bool> ExecuteRandomSkillAsync(CancellationToken ct)
    {
        _cachedReadySkills.Clear();

        foreach (var pair in _skillMap)
        {
            if (pair.Value.CanExecute(_currentTarget))
            {
                _cachedReadySkills.Add(pair.Key);
            }
        }

        if (_cachedReadySkills.Count == 0) return false;

        SkillId pickedSkillId = _cachedReadySkills[UnityEngine.Random.Range(0, _cachedReadySkills.Count)];
        return await ExecuteSkillAsync(pickedSkillId, ct);
    }
    #endregion

    #region 피격 및 페이즈 판정
    public override void ApplyDamage(in DamageContext context)
    {
        if (IsDead || _blackboard.IsInvincible) return;

        _enemyStatus.TakeDamage(context.Damage, context.PoiseDamage);

        if ((_enemyStatus.CurrentHp / _enemyStatus.MaxHp) <= _blackboard.Phase2HpRatio && !_blackboard.HasExecutedPhase2Entry)
        {
            _blackboard.IsInvincible = true;

            if (_groggyModule.IsGroggy)
            {
                _groggyModule.ResetState();
                AsyncUtil.CancelAndDispose(ref _actionCts);
            }
        }
    }
    #endregion

    protected override INode.ENodeState ExecuteDeath()
    {
        return base.ExecuteDeath();
    }
}