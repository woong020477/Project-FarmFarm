// 역할: 기간 이벤트의 설명과 능력치 수정 목록을 작성하는 공유 정의.
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameEvent", menuName = "Farm/Game Event")]
public sealed class GameEventDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string eventId;
    [SerializeField]
    private string title;
    [TextArea]
    [SerializeField]
    private string description;
    [Header("Duration")]
    [Min(1f)]
    [SerializeField]
    private float durationSeconds = 600f;
    [Header("Modifiers")]
    [SerializeField]
    private List<StatModifier> modifiers = new();
    public string EventId => eventId;
    public string Title => title;
    public string Description => description;
    public float DurationSeconds => Mathf.Max(1f, durationSeconds);
    public IReadOnlyList<StatModifier> Modifiers => modifiers;
}
