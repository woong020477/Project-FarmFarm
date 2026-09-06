// 역할: 능력치 종류·대상·증감률을 표현하는 이벤트 항목.
using System;
using UnityEngine;

[Serializable]
public sealed class StatModifier
{
    [SerializeField]
    private StatType stat;
    [SerializeField]
    private string targetId = StatTargets.All;
    [SerializeField]
    private float percent;
    public StatType Stat => stat;
    public string TargetId => string.IsNullOrWhiteSpace(targetId) ? StatTargets.All : targetId;
    public float Percent => percent;

    public StatModifier(StatType stat, string targetId, float percent)
    {
        this.stat = stat;
        this.targetId = string.IsNullOrWhiteSpace(targetId) ? StatTargets.All : targetId;
        this.percent = percent;
    }

    public bool AppliesTo(StatType requestedStat, string requestedTargetId)
    {
        if (stat != requestedStat)
            return false;
        string normalizedTargetId = string.IsNullOrWhiteSpace(requestedTargetId) ? StatTargets.All : requestedTargetId;
        return TargetId == StatTargets.All || TargetId == normalizedTargetId;
    }
}
