using System;

/// <summary>
/// 스탯에 주는 영향을 표현하는 기본 데이터 구조
/// </summary>
public readonly struct StatModifier
{
    public readonly float Value;
    public readonly StatModType Type;

    public StatModifier(float value, StatModType type)
    {
        this.Value = value;
        this.Type = type;
    }
}

/// <summary>
/// 개별 스탯 수치 계산기. 스탯 하나의 기본값과 수정자 목록을 들고 있으며, 수정자가 변경될 때만 캐시를 재계산하는 순수 C# 클래스
/// 값이 변경될 때만 재계산하는 더티 플래그(Dirty Flag) 패턴 사용
/// </summary>
[Serializable]
public class ModifiableStat
{
    public float BaseValue { get; set; }

    private readonly System.Collections.Generic.List<StatModifier> _modifiers = new System.Collections.Generic.List<StatModifier>();
    private float _cachedValue;
    private bool _isDirty = true;

    public float Value
    {
        get
        {
            if(_isDirty)
            {
                _cachedValue = CacluateFinalValue();
                _isDirty = false;
            }
            return _cachedValue;
        }
    }

    public ModifiableStat(float baseValue)
    {
        this.BaseValue = baseValue;
    }

    public void AddModifier(StatModifier modifier)
    {
        _modifiers.Add(modifier);
        _isDirty = true;
    }

    public void RemoveModifier(StatModifier modifier)
    {
        if (_modifiers.Remove(modifier))
            _isDirty = true;
    }

    public void ClearModifiers()
    {
        _modifiers.Clear();
        _isDirty = true;
    }

    private float CacluateFinalValue()
    {
        float finalValue = BaseValue;
        float percentSum = 0f;

        for(int i=0;i< _modifiers.Count; i++)
        {
            if (_modifiers[i].Type == StatModType.Flat)
                finalValue += _modifiers[i].Value;
            else if (_modifiers[i].Type == StatModType.PercentMult)
                percentSum += _modifiers[i].Value;
        }

        finalValue *= (1.0f + percentSum);
        return finalValue;
    }
} 