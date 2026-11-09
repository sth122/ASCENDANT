public static class CombatFormula
{
    /// <summary>
    /// 소울라이크 표준 5구간 비선형 방어력 관통 연산 (x = AR_eff / DEF)
    /// 부호 및 분모 누락으로 인한 음수 대미지와 폭증 버그를 정밀 교정
    /// </summary>
    public static float CalculatePenetratedDamage(float effectiveAR, float targetFlatDef)
    {
        // 방어력이 0 이하인 경우 방어율 90% 관통 기본 적용
        if (targetFlatDef <= 0.01f) return effectiveAR * 0.90f;

        float x = effectiveAR / targetFlatDef;

        // 1구간: x < 0.125 (초고방어 대상, 10% 최소 피해)
        if (x < 0.125f) return effectiveAR * 0.10f;

        // 2구간: 0.125 <= x < 1.0 (분모 49.0f 정규화 계수 적용)
        if (x < 1.0f)
        {
            float polynomial = (19.2f / 49.0f) * UnityEngine.Mathf.Pow(x - 0.125f, 2f) + 0.10f;
            return effectiveAR * polynomial;
        }

        // 3구간: 1.0 <= x < 2.5 (음수 대미지 방지: 계수 -0.1333f 적용)
        if (x < 2.5f)
        {
            float polynomial = -0.1333f * UnityEngine.Mathf.Pow(x - 2.5f, 2f) + 0.70f;
            return effectiveAR * polynomial;
        }

        // 4구간: 2.5 <= x < 8.0 (분모 121.0f 포화 곡선 적용)
        if (x < 8.0f)
        {
            float polynomial = (-0.8f / 121.0f) * UnityEngine.Mathf.Pow(x - 8.0f, 2f) + 0.90f;
            return effectiveAR * polynomial;
        }

        // 5구간: x >= 8.0 (완전 관통 상한선 90%)
        return effectiveAR * 0.90f;
    }
}