using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// 특정 Enemy 타입이 보유한 모든 공격 패턴 목록을 관리하는 테이블 ScriptableObject
/// 인스펙터에서는 List로 편집하고, 런타임에는 AnimStateType을 키로 하는 O(1) 딕셔너리로 변환
/// </summary>
[CreateAssetMenu(fileName = "EnemyAttackDatabase", menuName = "Unit/EnemyAttackDatabaseSO")]
public class EnemyAttackDatabaseSO : ScriptableObject
{
    [Serializable]
    public struct AttackEntry
    {
        [Tooltip("애니메이션 상태 Enum 키")]
        public AnimStateType AnimState;
        [Tooltip("해당 공격의 세부 수치")]
        public EnemyAttackData AttackData;
    }

    [Serializable]
    public struct UnitEntry
    {
        [Tooltip("몬스터 식별 UnitId 키")]
        public UnitId UnitIdentifier;
        [Tooltip("해당 유닛이 보유한 공격 목록")]
        public List<AttackEntry> Attacks;
    }

    [Header("Enemy 전체 공격 테이블 직렬화 목록")]
    [SerializeField] private List<UnitEntry> _databaseEntries = new List<UnitEntry>();

    private readonly Dictionary<UnitId, Dictionary<AnimStateType, EnemyAttackData>> _unitAttackDatabase
        = new Dictionary<UnitId, Dictionary<AnimStateType, EnemyAttackData>>();

    private bool _isInitialized = false;

    /// <summary>
    /// 인스펙터 직렬화 리스트를 순회하여 이중 딕셔너리 캐시 구축
    /// </summary>
    public void InitDatabase()
    {
        _unitAttackDatabase.Clear();

        for (int i = 0; i < _databaseEntries.Count; i++)
        {
            UnitEntry unitEntry = _databaseEntries[i];
            UnitId unitKey = unitEntry.UnitIdentifier;

            if (!_unitAttackDatabase.ContainsKey(unitKey))
            {
                _unitAttackDatabase.Add(unitKey, new Dictionary<AnimStateType, EnemyAttackData>());
            }
            else
            {
                DebugLogger.LogWarningWithTag(name, $"중복된 UnitId 등록 시도: {unitKey}");
                continue;
            }

            if (unitEntry.Attacks == null) continue;

            for (int j = 0; j < unitEntry.Attacks.Count; j++)
            {
                AttackEntry attackEntry = unitEntry.Attacks[j];
                AnimStateType animKey = attackEntry.AnimState;

                if (!_unitAttackDatabase[unitKey].ContainsKey(animKey))
                {
                    _unitAttackDatabase[unitKey].Add(animKey, attackEntry.AttackData);
                }
                else
                {
                    DebugLogger.LogWarningWithTag(name, $"[{unitKey}] 중복된 AnimStateType 등록 시도: {animKey}");
                }
            }
        }
        _isInitialized = true;
    }


    /// <summary>
    /// UnitId와 AnimStateType 이중 키를 통해 공격 판정 데이터를 O(1)로 안전하게 조회
    /// </summary>
    /// <param name="unitId">몬스터 고유 ID</param>
    /// <param name="animState">재생할 애니메이션 상태</param>
    /// <param name="attackData">조회된 공격 사양 struct</param>
    /// <returns>데이터 존재 여부</returns>
    public bool TryGetAttackData(UnitId unitId, AnimStateType animState, out EnemyAttackData attackData)
    {
        if (!_isInitialized)
        {
            InitDatabase();
        }

        attackData = default;

        if (_unitAttackDatabase.TryGetValue(unitId, out Dictionary<AnimStateType, EnemyAttackData> unitAttacks))
        {
            return unitAttacks.TryGetValue(animState, out attackData);
        }

        return false;
    }

    /// <summary>
    /// 특정 몬스터가 가진 모든 공격 패턴 목록을 조회 (AI 패턴 선택 루프용)
    /// </summary>
    public bool TryGetAllAttacksForUnit(UnitId unitId, out Dictionary<AnimStateType, EnemyAttackData> unitAttacks)
    {
        if (!_isInitialized)
        {
            InitDatabase();
        }

        return _unitAttackDatabase.TryGetValue(unitId, out unitAttacks);
    }

    private void OnValidate() => _isInitialized = false;
}
