using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using Util = DebugLogger<DragonBT>;

/// <summary>
/// 완성형 드래곤 보스 컨트롤러
/// </summary>
public class DragonBT : BossBTController
{
    private DragonCombatController _dragonCombat;
    public DragonCombatController DragonCombat => _dragonCombat;

    private bool _isPhase2Transitioning = false;

    protected override void SetupComponents()
    {
        _unitId = UnitId.Dragon;

        _combatController = GetComponent<DragonCombatController>();
        if (_combatController == null)
        {
            _combatController = gameObject.AddComponent<DragonCombatController>();
        }
        _dragonCombat = _combatController as DragonCombatController;

        base.SetupComponents();
    }
    public override void BindStatusData(EnemyInfo overrideStat = null)
    {
        _isPhase2Transitioning = false;
        base.BindStatusData(overrideStat);
    }

    protected override INode BuildBehaviorTree()
    {
        var root = new SelectorNode();

        // 1. 사망 판정
        var deathSeq = new SequenceNode();
        deathSeq.Add(new PredicateNode<DragonBT>(this, static b => b._enemyStatus != null && b._enemyStatus.CurrentHp <= 0f));
        deathSeq.Add(new ActionNode(ExecuteDeath));
        root.Add(deathSeq);

        // 2. 2페이즈 전환 (일반 전투 중 체력 임계점 도달 또는 그로기 종료 후 대기 플래그 발동 시 단일 진입)
        var phase2Seq = new SequenceNode();
        phase2Seq.Add(new PredicateNode<DragonBT>(this, static b =>
        {
            if (b._isPhase2Transitioning) return true;
            if (b._enemyStatus == null || b._blackboard.HasExecutedPhase2Entry || b._enemyStatus.HasTriggeredPhase2) return false;
            if (b._groggyModule.IsGroggy) return false; // 그로기 중에는 그로기가 완전히 끝날 때까지 2페이즈 모션 보류

            return b._enemyStatus.IsPhase2Pending ||
                   (b._enemyStatus.MaxHp > 0f && (b._enemyStatus.CurrentHp / b._enemyStatus.MaxHp) <= b._blackboard.Phase2HpRatio);
        }));
        phase2Seq.Add(new AsyncActionNode(ExecutePhase2TransitionAsync, GetActionToken));
        root.Add(phase2Seq);

        // 3. 그로기
        var groggySeq = new SequenceNode();
        groggySeq.Add(new PredicateNode<DragonBT>(this, static b =>
            !b._isPhase2Transitioning && b._enemyStatus != null && (b._groggyModule.IsGroggy || b._enemyStatus.IsPoiseBroken)));
        groggySeq.Add(new ActionNode(TickGroggyStatus));
        root.Add(groggySeq);

        // 4. 스킬 발동 브랜치
        var skillSeq = new SequenceNode();
        skillSeq.Add(new PredicateNode<DragonBT>(this, static b =>
        {
            if (b.IsDead || b._groggyModule.IsGroggy || b._isPhase2Transitioning) return false;

            if (Time.time >= b._blackboard.NextSkillCycleTime && b.HasAnyReadySkill(b._currentTarget))
            {
                b._blackboard.IsSkillPending = true;
            }

            if (b.IsActionExecuting) return false;

            return b._blackboard.IsSkillPending && b.HasAnyReadySkill(b._currentTarget);
        }));
        skillSeq.Add(new AsyncActionNode(ExecuteRandomSkillAsync, GetActionToken));
        root.Add(skillSeq);

        // 5. 기본 공격 및 추격 브랜치
        var combatSelector = new SelectorNode();

        var basicAttackSeq = new SequenceNode();
        basicAttackSeq.Add(new PredicateNode<DragonBT>(this, static b =>
            !b.IsActionExecuting && !b._isPhase2Transitioning && !b._blackboard.IsSkillPending &&
            b._combatController.IsGlobalCooldownReady && b._combatController.HasAnyReadyAttack(b._currentTarget)));
        basicAttackSeq.Add(new AsyncActionNode(ExecuteSelectedBasicAttackAsync, GetActionToken));
        combatSelector.Add(basicAttackSeq);

        var chaseSeq = new SequenceNode();
        chaseSeq.Add(new PredicateNode<DragonBT>(this, static b => !b.IsActionExecuting && !b._isPhase2Transitioning && b._currentTarget != null));
        chaseSeq.Add(new ActionNode(TickChasePlayer));
        combatSelector.Add(chaseSeq);

        root.Add(combatSelector);

        return root;
    }
    public override void TriggerGroggy()
    {
        // 2페이즈 돌입 모션(무적) 중에는 그로기로 인터럽트되지 않음
        if (_isPhase2Transitioning || (_enemyStatus != null && _enemyStatus.IsInvincible)) return;
        base.TriggerGroggy();
    }
    #region 전투 평가 및 실행
    protected override async UniTask<bool> ExecuteSelectedBasicAttackAsync(CancellationToken ct)
    {
        if (_currentTarget == null || IsDead || IsActionExecuting) return false;

        if (!_combatController.TryPickValidAttack(_currentTarget, out EnemyAttackData chosenData))
        {
            return false;
        }

        SetActionState(EnemyActionState.BasicAttacking);
        _combatController.ReserveGlobalCooldown(chosenData.AttackCooldown);
        _movement.StopMovement();

        try
        {
            if (chosenData.WindupDelay > 0.05f)
            {
                float elapsed = 0f;
                while (elapsed < chosenData.WindupDelay)
                {
                    ct.ThrowIfCancellationRequested();
                    if (_currentTarget != null) _movement.FaceTarget(_currentTarget.position, 12.0f, Time.deltaTime);
                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }

            float baseAtk = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.AttackPower : 45.0f;
            float basePoise = _enemyStatus.EnemyInfoSource != null ? _enemyStatus.EnemyInfoSource.BasePoiseDamage : 25.0f;
            float speedMod = _blackboard.IsPhase2 ? _blackboard.Phase2StatBuffMultiplier : 1.0f;

            Func<CancellationToken, UniTask> customEvent = null;
            if (chosenData.AttackAnimStateType == AnimStateType.Attack2)
            {
                customEvent = async (token) =>
                {
                    _dragonCombat?.TriggerEarthShatter(chosenData.CalculateFinalDamage(baseAtk), chosenData.CalculateFinalPoiseDamage(basePoise));
                    await UniTask.CompletedTask;
                };
            }

            await _combatController.ExecuteAttackTimelineAsync(chosenData, _currentTarget, baseAtk, basePoise, speedMod, customEvent, ct);
        }
        catch (OperationCanceledException) { return false; }
        finally
        {
            _combatController.CloseAllHitboxes();

            if (!IsDead && ActionState == EnemyActionState.BasicAttacking)
            {
                _enemyAnim.PlayState(AnimStateType.Idle, force: true);
                _combatController.RecordAttackCooldown(chosenData.AttackAnimStateType, chosenData.AttackCooldown);
                SetActionState(EnemyActionState.None);
            }
        }

        return true;
    }

    private INode.ENodeState TickChasePlayer()
    {
        if (_currentTarget == null) return INode.ENodeState.ENS_Failure;

        float chaseSpeed = _enemyStatus.EnemyMotionStatus != null ? _enemyStatus.EnemyMotionStatus.ChaseSpeed.Value : 4.5f;
        float speed = chaseSpeed * (_blackboard.IsPhase2 ? _blackboard.Phase2StatBuffMultiplier : 1.0f);
        _movement.SetDestination(_currentTarget.position, speed);
        _movement.TickLocomotionRotation(Time.deltaTime, 4.0f);
        _enemyAnim.PlayState(AnimStateType.Walk);

        return INode.ENodeState.ENS_Running;
    }
    #endregion

    #region 페이즈 2 전환 시퀀스
    private async UniTask<bool> ExecutePhase2TransitionAsync(CancellationToken ct)
    {
        if (_blackboard.HasExecutedPhase2Entry) return false;

        // 1. 진입 즉시 재진입 차단 및 무적 활성화 (BossHUD 체력바 은백색 전환)
        _isPhase2Transitioning = true;
        _blackboard.HasExecutedPhase2Entry = true;
        _enemyStatus.MarkPhase2Triggered();
        _enemyStatus.ResetPoiseToDefault();

        _enemyStatus.SetInvincible(true);
        _blackboard.IsInvincible = true;

        _combatController.CloseAllHitboxes();
        SetActionState(EnemyActionState.SkillCasting);
        _movement.StopMovement();

        Util.Log("[Phase 2] 무적화(은색 체력바) 및 2페이즈 진입 연출 시작");

        try
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayBGM(SoundId.BGM_Boss_Phase2, fadeDuration: 1.5f);
            }

            _movement.BeginRootMotionMovement();
            _enemyAnim.SetRootMotion(true, (deltaPos, deltaRot) => _movement.ApplyRootMotionDelta(deltaPos, deltaRot, isAirborne: true));
            await PlayStateAndWaitAsync(AnimStateType.Dragon_Takeoff_InPlace, ct, force: true);

            _enemyStatus.HealHp(_enemyStatus.MaxHp * _blackboard.Phase2HealRatio);
            await PlayStateAndWaitAsync(AnimStateType.Dragon_HoverLoop, ct, force: true);

            await PlayStateAndWaitAsync(AnimStateType.Dragon_Landing_InPlace, ct, force: true);
            _enemyAnim.SetRootMotion(false);
            _movement.EndRootMotionMovement();

            await PlayStateAndWaitAsync(AnimStateType.Berserk, ct, force: true);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _enemyAnim.SetRootMotion(false);
            _movement.EndRootMotionMovement();

            // 2. 연출 종료 시 무적 반드시 해제 (BossHUD 체력바 붉은색 복귀)
            _enemyStatus.SetInvincible(false);
            _blackboard.IsInvincible = false;

            _enemyStatus.ResetPoiseToDefault();
            _blackboard.IsPhase2 = true;
            _blackboard.ResetSkillCycle();
            _isPhase2Transitioning = false;

            if (!IsDead)
            {
                _enemyAnim.PlayState(AnimStateType.Idle, force: true);
                SetActionState(EnemyActionState.None);
            }

            Util.Log("[Phase 2] 2페이즈 전환 완료 및 무적 해제");
        }

        return true;
    }
    #endregion

    private void HandleGroggyEnded()
    {
        // 그로기 도중 2페이즈 기준선에 도달하여 무적 대기 중이었던 경우
        if (_enemyStatus != null && _enemyStatus.IsPhase2Pending)
        {
            _enemyStatus.ClearPhase2Pending();
            _blackboard.IsPhase2 = true;

            // 즉각 2페이즈 전환 비동기 실행
            ExecutePhase2TransitionAsync(this.destroyCancellationToken).Forget();
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_groggyModule != null)
        {
            _groggyModule.OnGroggyEnded -= HandleGroggyEnded;
        }
    }
}