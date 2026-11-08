using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using Util = DebugLogger;

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

    protected override INode BuildBehaviorTree()
    {
        var root = new SelectorNode();

        // 1. 사망 판정
        var deathSeq = new SequenceNode();
        deathSeq.Add(new PredicateNode<DragonBT>(this, static b => b._enemyStatus != null && b._enemyStatus.CurrentHp <= 0f));
        deathSeq.Add(new ActionNode(ExecuteDeath));
        root.Add(deathSeq);

        // 2. 2페이즈 전환 (최우선 순위 인터럽트)
        var phase2Seq = new SequenceNode();
        phase2Seq.Add(new PredicateNode<DragonBT>(this, static b =>
            b._isPhase2Transitioning || (!b._blackboard.HasExecutedPhase2Entry && b._enemyStatus != null &&
             (b._enemyStatus.CurrentHp / b._enemyStatus.MaxHp) <= b._blackboard.Phase2HpRatio)));
        phase2Seq.Add(new AsyncActionNode(ExecutePhase2TransitionAsync, GetActionToken));
        root.Add(phase2Seq);

        // 3. 그로기
        var groggySeq = new SequenceNode();
        groggySeq.Add(new PredicateNode<DragonBT>(this, static b =>
            b._enemyStatus != null && b._enemyStatus.IsPoiseBroken && !b._groggyModule.IsGroggy));
        groggySeq.Add(new ActionNode(TriggerGroggyAction));
        root.Add(groggySeq);

        // 4. 스킬 발동 브랜치 (기본 공격 중에는 진입 불가)
        var skillSeq = new SequenceNode();
        skillSeq.Add(new PredicateNode<DragonBT>(this, static b =>
        {
            if (b.IsDead || b._groggyModule.IsGroggy || b._isPhase2Transitioning) return false;

            // 스킬 쿨타임 주기 도달 및 사거리 내 유효 스킬 존재 시 대기 플래그 셋
            if (Time.time >= b._blackboard.NextSkillCycleTime && b.HasAnyReadySkill(b._currentTarget))
            {
                b._blackboard.IsSkillPending = true;
            }

            // [상호 배타 핵심 가드] 기본 공격이 진행 중이면 스킬을 절대 시전하지 않고 평타 완주를 대기
            if (b.IsActionExecuting) return false;

            return b._blackboard.IsSkillPending && b.HasAnyReadySkill(b._currentTarget);
        }));
        skillSeq.Add(new AsyncActionNode(ExecuteRandomSkillAsync, GetActionToken));
        root.Add(skillSeq);

        // 5. 기본 공격 및 추격 브랜치
        var combatSelector = new SelectorNode();

        var basicAttackSeq = new SequenceNode();
        basicAttackSeq.Add(new PredicateNode<DragonBT>(this, static b =>
            !b.IsActionExecuting && !b._blackboard.IsSkillPending && b._combatController.IsGlobalCooldownReady &&
            b._combatController.HasAnyReadyAttack(b._currentTarget)));
        basicAttackSeq.Add(new AsyncActionNode(ExecuteSelectedBasicAttackAsync, GetActionToken));
        combatSelector.Add(basicAttackSeq);

        var chaseSeq = new SequenceNode();
        chaseSeq.Add(new PredicateNode<DragonBT>(this, static b => !b.IsActionExecuting && b._currentTarget != null));
        chaseSeq.Add(new ActionNode(TickChasePlayer));
        combatSelector.Add(chaseSeq);

        root.Add(combatSelector);

        return root;
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

            if (!IsDead)
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
        _combatController.CloseAllHitboxes();

        _isPhase2Transitioning = true;
        SetActionState(EnemyActionState.None);
        _blackboard.IsInvincible = true;
        _movement.StopMovement();

        Util.LogWithTag("BossDragon", "[Phase 2] 무적화 및 공중 체공 진입");

        try
        {
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
        finally
        {
            _enemyAnim.SetRootMotion(false);
            _movement.EndRootMotionMovement();

            _enemyStatus.RestorePoiseToMax();
            _blackboard.IsPhase2 = true;
            _blackboard.HasExecutedPhase2Entry = true;
            _blackboard.IsInvincible = false;
            _blackboard.ResetSkillCycle();
            _isPhase2Transitioning = false;
            SetActionState(EnemyActionState.None);
        }

        return true;
    }
    #endregion
}