using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Util = DebugLogger;

/// <summary>
/// 드래곤 보스 완성형 BT 컨트롤러
/// 순찰/복귀 없이 상시 플레이어 추적 및 6단계 우선순위 셀렉터로 구동됩니다.
/// </summary>
public class DragonBT : BossBTController
{
    protected override void SetupComponents()
    {
        _unitId = UnitId.Dragon;
        base.SetupComponents();
    }

    protected override INode BuildBehaviorTree()
    {
        var root = new SelectorNode();

        // 1. 사망 판정 노드
        var deathSeq = new SequenceNode();
        deathSeq.Add(new PredicateNode<DragonBT>(this, static (boss) => boss._enemyStatus != null && boss._enemyStatus.CurrentHp <= 0f));
        deathSeq.Add(new AsyncActionNode(ExecuteDeathAsync, GetActionToken));
        root.Add(deathSeq);

        // 2. 2페이즈 전환 브랜치
        var phase2Seq = new SequenceNode();
        phase2Seq.Add(new PredicateNode<DragonBT>(this, static (boss) =>
            !boss._blackboard.HasExecutedPhase2Entry &&  boss._enemyStatus != null &&
            (boss._enemyStatus.CurrentHp / boss._enemyStatus.MaxHp) <= boss._blackboard.Phase2HpRatio &&
            (!boss._isActionExecuting || boss._blackboard.IsInvincible)
        ));
        phase2Seq.Add(new AsyncActionNode(ExecutePhase2TransitionAsync, GetActionToken));
        root.Add(phase2Seq);

        // 3. 그로기 브랜치
        var groggySeq = new SequenceNode();
        groggySeq.Add(new PredicateNode<DragonBT>(this, static (boss) =>
            boss._enemyStatus != null && boss._enemyStatus.IsPoiseBroken && !boss._isGroggy));
        groggySeq.Add(new AsyncActionNode(ExecuteGroggyAsync, GetActionToken));
        root.Add(groggySeq);

        // 4. 액션 실행 잠금 노드 (비동기 패턴 실행 중 트리 평가 차단)
        root.Add(new PredicateNode<DragonBT>(this, static (boss) => boss._blackboard.IsActionExecuting));

        // 5. 15초 주기 스킬 발동 브랜치
        var skillSeq = new SequenceNode();
        skillSeq.Add(new PredicateNode<DragonBT>(this, static (boss) =>
            (Time.time >= boss._blackboard.NextSkillCycleTime || boss._blackboard.IsSkillPending) &&
            boss.HasAnyReadySkill()
        ));
        skillSeq.Add(new AsyncActionNode(ExecuteRandomSkillAsync, GetActionToken));
        root.Add(skillSeq);

        // 6. 기본 전투 및 추격 브랜치 (UnitCombatDatabaseSO 평타 동적 추첨)
        var combatSelector = new SelectorNode();

        // 6-1. UnitCombatDatabaseSO에 등록된 평타 중 사거리/쿨타임 충족 패턴 실행
        var basicAttackSeq = new SequenceNode();
        basicAttackSeq.Add(new PredicateNode<DragonBT>(this, static (boss) =>
            Time.time >= boss._globalAttackCooldownTime && boss.HasAnyReadyAttack()));
        basicAttackSeq.Add(new AsyncActionNode(ExecuteSelectedBasicAttackAsync, GetActionToken));
        combatSelector.Add(basicAttackSeq);

        // 6-2. 사거리 밖: 플레이어 상시 추격
        var chaseSeq = new SequenceNode();
        chaseSeq.Add(new PredicateNode<DragonBT>(this, static (boss) => boss._currentTarget != null));
        chaseSeq.Add(new ActionNode(TickChasePlayer));
        combatSelector.Add(chaseSeq);

        root.Add(combatSelector);

        return root;
    }

    #region AI 실행 서브 루틴
    private INode.ENodeState TickChasePlayer()
    {
        if (_currentTarget == null) return INode.ENodeState.ENS_Failure;

        float chaseSpeed = _enemyStatus.EnemyMotionStatus != null
            ? _enemyStatus.EnemyMotionStatus.ChaseSpeed.Value : 4.5f;

        float speed = chaseSpeed * (_blackboard.IsPhase2 ? _blackboard.Phase2StatBuffMultiplier : 1.0f);
        _movement.SetDestination(_currentTarget.position, speed);
        _movement.TickLocomotionRotation(Time.deltaTime, 4.0f);
        _enemyAnim.PlayState(AnimStateType.Walk);

        return INode.ENodeState.ENS_Running;
    }

    /// <summary>
    /// 페이즈 2 전환 시퀀스
    /// </summary>
    private async UniTask<bool> ExecutePhase2TransitionAsync(CancellationToken ct)
    {
        _blackboard.IsActionExecuting = true;
        _blackboard.HasExecutedPhase2Entry = true;
        _blackboard.IsInvincible = true;
        _movement.StopMovement();
        CloseAllHitboxes();

        Util.LogWithTag("BossDragon", "[Phase 2] 무적화 및 공중 체공 연출 진입");
        try
        {
            // 1. Dragon_Takeoff_InPlace : 제자리 도약
            await PlayStateAndWaitAsync(AnimStateType.Dragon_Takeoff_InPlace, ct, force: true);

            // 2. Dragon_HoverLoop : 공중 체공 호버링 + 체력 10% 회복
            _enemyStatus.HealHp(_enemyStatus.MaxHp * _blackboard.Phase2HealRatio);
            await PlayStateAndWaitAsync(AnimStateType.Dragon_HoverLoop, ct, force: true);

            // 3. Dragon_Landing_InPlace : 제자리 착지
            await PlayStateAndWaitAsync(AnimStateType.Dragon_Landing_InPlace, ct, force: true);

            // 4. 바닥 착지 후 2페이즈 광폭화 포효 연출
            await PlayStateAndWaitAsync(AnimStateType.Berserk, ct, force: true);
        }
        finally
        {
            _enemyStatus.RestorePoiseToMax();
            _blackboard.IsPhase2 = true;
            _blackboard.IsInvincible = false;
            _blackboard.ResetSkillCycle();
            _blackboard.IsSkillPending = false;
            _blackboard.IsActionExecuting = false;
        }

        Util.LogWithTag("BossDragon", "[Phase 2 완료] 드래곤 광폭화 개시");
        return true;
    }

    private async UniTask<bool> ExecuteDeathAsync(CancellationToken ct)
    {
        SetControllerActive(false);
        _movement.StopMovement();
        CloseAllHitboxes();

        _enemyAnim.PlayState(AnimStateType.Death);
        EventBusSystem.Publish(new UnitDiedEvent(gameObject, _unitId));

        await UniTask.Delay(TimeSpan.FromSeconds(5.0f), cancellationToken: ct);
        return true;
    }
    #endregion
}