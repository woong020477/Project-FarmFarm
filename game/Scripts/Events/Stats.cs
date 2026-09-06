// 역할: 이벤트 배율을 적용할 때 사용하는 능력치 계산 규칙.
using System.Collections.Generic;
using UnityEngine;

public static class Stats
{
    public static float GetMultiplier(IReadOnlyList<StatModifier> modifiers, StatType stat, string targetId = StatTargets.All)
    {
        float totalPercent = 0f;
        if (modifiers != null)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier modifier = modifiers[i];
                if (modifier != null && modifier.AppliesTo(stat, targetId))
                    totalPercent += modifier.Percent;
            }
        }

        return Mathf.Max(0f, 1f + totalPercent * 0.01f);
    }

    public static int ApplyPrice(int basePrice, float multiplier)
    {
        return Mathf.Max(1, Mathf.RoundToInt(basePrice * Mathf.Max(0f, multiplier)));
    }
}
