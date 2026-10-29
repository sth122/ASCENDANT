using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Util = DebugLogger;

/// <summary>
/// 모든 보스가 공유하는 최상위 BT 컨트롤러
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
        base.SetupComponents();

        // 조우 컷신 트리거 전까지는 틱 정지
        SetControllerActive(false);
        _groggyRecoveryDuration = 20f;
    }

    public override void Init()
    {
        base.Init();
        // 비동기 SO 적재 완료 대기 및 스킬/스탯 바인딩 보장
        EnsureDatabaseAndSkillsAsync(this.destroyCancellationToken).Forget();
    }

    #region SO 스탯 초기화 파이프라인
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

            // 블랙보드 기획 파라미터 동기화
            _blackboard.Phase2HpRatio = runtimeBossInfo.Phase2ThresholdRatio;
            _blackboard.Phase2HealRatio = runtimeBossInfo.Phase2HealRatio;
            _blackboard.Phase2StatBuffMultiplier = runtimeBossInfo.Phase2StatBuffMultiplier;
            _blackboard.SkillCycleInterval = runtimeBossInfo.SkillCycleInterval;
            _blackboard.SkillCooldownResetChance = runtimeBossInfo.SkillCooldownResetChance;

            _blackboard.ResetSkillCycle();
            Util.LogWithTag(name, $"[{_unitId}] BossInfo SO 데이터 바인딩 및 블랙보드 수치 동기화 완료");
        }
    }

    private async UniTaskVoid EnsureDatabaseAndSkillsAsync(CancellationToken ct)
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
                Util.LogWithTag(name, $"[{_unitId}] BossSkill {skillDataList.Count}종 바인딩 완료");
            }
        }
    }
    #endregion

    public override void OnParried(float parryPoiseDamage = 35.0f)
    {
        if (IsDead) return;

        // 보스는 패링당해도 모션 경직(ParryStagger)을 겪지 않고 패턴을 계속 진행
        _enemyStatus.TakeDamage(0f, 10.0f);
        Util.LogWithTag(name, "[보스 패링 피격] 경직 면역: 패턴 유지");
    }

    /// <summary>
    /// 플레이어 보스룸 진입 시 조우 연출 실행
    /// </summary>
    public virtual void TriggerEncounterSequence(Transform playerTransform) => ExecuteEncounterAsync(playerTransform, GetActionToken()).Forget();

    private async UniTaskVoid ExecuteEncounterAsync(Transform playerTransform, CancellationToken token)
    {
        Util.LogWithTag(name, "[보스 조우 시퀀스 개시] 시네마틱 카메라 전환");

        _blackboard.IsInvincible = true;

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.PlayBossCutscene(transform);
        }

        transform.position = _spawnPoint;
        _movement.StopMovement();
        _enemyAnim.PlayState(AnimStateType.Roar);

        SetTarget(playerTransform);

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3.0f), cancellationToken: token);
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
            Util.LogWithTag(name, "[보스전 개시] BT 평가 가동");
        }
    }

    #region ISkillable 구현부
    public bool CanExecuteSkill(SkillId skillId) => _skillMap.TryGetValue(skillId, out BossSkill skill) && skill.CanExecute();

    public async UniTask<bool> ExecuteSkillAsync(SkillId skillId, CancellationToken ct)
    {
        if (_skillMap.TryGetValue(skillId, out BossSkill skill))
        {
            _blackboard.IsActionExecuting = true;
            _movement.StopMovement();

            bool success = false;
            try
            {
                success = await skill.ExecuteAsync(ct);
            }
            finally
            {
                // 페이즈 2 30% 즉시 쿨타임 초기화 판정
                if (_blackboard.IsPhase2 && UnityEngine.Random.value <= _blackboard.SkillCooldownResetChance)
                {
                    skill.ResetCooldown();
                    _blackboard.ForceReadySkillCycle();
                    Util.LogWithTag(name, $"[{skillId}] 30% 확률 즉시 쿨타임 초기화 발동");
                }
                else
                {
                    skill.TriggerCooldown();
                    _blackboard.ResetSkillCycle();
                }

                _blackboard.IsActionExecuting = false;
            }

            return success;
        }
        return false;
    }

    public void CancelSkill()
    {
        CloseAllHitboxes();
        _blackboard.IsActionExecuting = false;
        AsyncUtil.CancelAndDispose(ref _actionCts);
    }
    #endregion

    #region 스킬 평가
    /// <summary>
    /// 현재 사용 가능한 스킬이 존재하는 검사
    /// </summary
    protected bool HasAnyReadySkill()
    {
        foreach (var pair in _skillMap)
        {
            if (pair.Value.CanExecute()) return true;
        }
        return false;
    }

    /// <summary>
    /// 현재 시전 조건을 만족하는 스킬 중 1개를 시전
    /// </summary>
    protected async UniTask<bool> ExecuteRandomSkillAsync(CancellationToken ct)
    {
        _cachedReadySkills.Clear();

        foreach (var pair in _skillMap)
        {
            if (pair.Value.CanExecute())
            {
                _cachedReadySkills.Add(pair.Key);
            }
        }

        if (_cachedReadySkills.Count == 0) return false;

        SkillId pickedSkillId = _cachedReadySkills[UnityEngine.Random.Range(0, _cachedReadySkills.Count)];
        return await ExecuteSkillAsync(pickedSkillId, ct);
    }
    #endregion

    #region IDamageableTest 피격 수신부
    public override void ApplyDamage(in DamageContext context)
    {
        if (IsDead) return;

        // 무적 상태 시 대미지 및 포이즈 완전 무효화
        if (_blackboard.IsInvincible)
        {
            Util.LogWithTag(name, "[무적 방어] 페이즈 연출 중 공격 무효화");
            return;
        }

        _enemyStatus.TakeDamage(context.Damage, context.PoiseDamage);

        // 2페이즈 체력 조건 도달 검사
        if ((_enemyStatus.CurrentHp / _enemyStatus.MaxHp) <= _blackboard.Phase2HpRatio && !_blackboard.HasExecutedPhase2Entry)
        {
            // 즉시 무적화
            _blackboard.IsInvincible = true;

            // 그로기 상태 도중 체력 도달 시: 그로기 즉각 취소 및 2페이즈 전환 시작
            if (_isGroggy)
            {
                Util.LogWithTag(name, "[2페이즈 인터럽트] 그로기 중 2페이즈 진입: 그로기 취소 및 비행 연출 돌입");
                AsyncUtil.CancelAndDispose(ref _actionCts);
                _isGroggy = false;
                CanBeRiposted = false;
            }
            else if (_blackboard.IsBasicAttacking || _blackboard.IsActionExecuting)
            {
                // 진행 중인 패턴이 끝나면 즉시 전환되도록 플래그 설정
                _blackboard.IsSkillPending = true;
            }
        }
    }
    #endregion
}