// 역할: 활성 기간 이벤트를 보관하고 대상 능력치에 적용할 배율을 제공한다.
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameEvents : MonoBehaviour
{
    private sealed class ActiveEvent
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public long EndUnixSeconds { get; }
        public IReadOnlyList<StatModifier> Modifiers { get; }
        public bool IsServerEvent { get; }

        public ActiveEvent(string id, string title, string description, long endUnixSeconds, IReadOnlyList<StatModifier> modifiers, bool isServerEvent)
        {
            Id = id;
            Title = title;
            Description = description;
            EndUnixSeconds = endUnixSeconds;
            Modifiers = modifiers;
            IsServerEvent = isServerEvent;
        }
    }

    public static GameEvents Instance { get; private set; }
    private ServerClock clock => GameManager.Instance != null ? GameManager.Instance.Clock : ServerClock.Instance;

    [Header("Debug Event")]
    [SerializeField]
    private GameEventDefinition startEvent;
    [SerializeField]
    private bool startEventOnPlay;
    private readonly List<ActiveEvent> activeEvents = new();
    private readonly List<StatModifier> activeModifiers = new();
    private readonly Dictionary<string, float> multiplierCache = new();
    private float nextExpiryCheckTime;
    public event Action Changed;
    public bool HasActiveEvent => activeEvents.Count > 0;

    public string DescribeActiveEvents()
    {
        if (activeEvents.Count == 0)
            return "이벤트 정보를 불러오는 중입니다.";
        var result = new System.Text.StringBuilder();
        foreach (var e in activeEvents)
        {
            result.AppendLine(e.Description).AppendLine(e.Title);
            result.AppendLine($"남은 시간 {Math.Max(0, e.EndUnixSeconds - CurrentUnixSeconds)}초");
            foreach (var m in e.Modifiers)
            {
                string target = m.TargetId == StatTargets.All ? "전체" : m.TargetId;
                if (GameManager.Instance != null)
                    foreach (var crop in GameManager.Instance.CropDefinitions)
                        if (crop.ServerId == m.TargetId)
                        {
                            target = crop.DisplayName;
                            break;
                        }

                string label = m.Stat switch
                {
                    StatType.PlayerSpeed => "플레이어 이동속도",
                    StatType.DroneSpeed => "드론 이동속도",
                    StatType.CropGrowth => "작물 성장속도",
                    StatType.CropPrice => "판매금액",
                    StatType.MercenaryCost => "병력 호출 비용",
                    StatType.ZombieHealth => "좀비 체력",
                    StatType.ZombieSpawnRate => "좀비 출현 빈도",
                    StatType.MercenarySpeed => "병력 이동속도",
                    StatType.MercenaryAttack => "병력 공격력",
                    StatType.MercenaryAttackSpeed => "병력 공격속도",
                    _ => m.Stat.ToString()};
                result.AppendLine($"{label} ({target}) {m.Percent:+0;-0;0}%");
            }

            result.AppendLine();
        }

        return result.ToString();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (startEventOnPlay && startEvent != null)
            Activate(startEvent);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextExpiryCheckTime)
            return;
        nextExpiryCheckTime = Time.unscaledTime + 0.5f;
        RemoveExpiredEvents();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Activate(GameEventDefinition definition)
    {
        if (definition == null)
            return;
        long endUnixSeconds = CurrentUnixSeconds + Mathf.CeilToInt(definition.DurationSeconds);
        AddOrReplace(new ActiveEvent(definition.EventId, definition.Title, definition.Description, endUnixSeconds, definition.Modifiers, false));
    }

    public void ApplyServerEvent(string eventId, string title, string description, long endUnixSeconds, IReadOnlyList<StatModifier> modifiers)
    {
        RemoveServerEvents(false);
        if (string.IsNullOrWhiteSpace(eventId) || endUnixSeconds <= CurrentUnixSeconds || modifiers == null)
        {
            RebuildModifiers();
            return;
        }

        activeEvents.Add(new ActiveEvent(eventId, title, description, endUnixSeconds, modifiers, true));
        RebuildModifiers();
    }

    public void ClearServerEvent()
    {
        RemoveServerEvents(true);
    }

    public float GetMultiplier(StatType stat, string targetId = StatTargets.All)
    {
        string cacheKey = $"{stat}:{targetId}";
        if (multiplierCache.TryGetValue(cacheKey, out float cachedMultiplier))
            return cachedMultiplier;
        float multiplier = Stats.GetMultiplier(activeModifiers, stat, targetId);
        multiplierCache.Add(cacheKey, multiplier);
        return multiplier;
    }

    private long CurrentUnixSeconds
    {
        get
        {
            if (clock != null && clock.IsReady)
                return clock.UnixTimeSeconds;
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    private void AddOrReplace(ActiveEvent activeEvent)
    {
        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            if (activeEvents[i].Id == activeEvent.Id)
                activeEvents.RemoveAt(i);
        }

        activeEvents.Add(activeEvent);
        RebuildModifiers();
    }

    private void RemoveExpiredEvents()
    {
        long now = CurrentUnixSeconds;
        bool removed = false;
        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            if (activeEvents[i].EndUnixSeconds > now)
                continue;
            activeEvents.RemoveAt(i);
            removed = true;
        }

        if (removed)
            RebuildModifiers();
    }

    private void RemoveServerEvents(bool rebuild)
    {
        bool removed = false;
        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            if (!activeEvents[i].IsServerEvent)
                continue;
            activeEvents.RemoveAt(i);
            removed = true;
        }

        if (removed && rebuild)
            RebuildModifiers();
    }

    private void RebuildModifiers()
    {
        activeModifiers.Clear();
        for (int i = 0; i < activeEvents.Count; i++)
        {
            IReadOnlyList<StatModifier> modifiers = activeEvents[i].Modifiers;
            for (int j = 0; j < modifiers.Count; j++)
            {
                if (modifiers[j] != null)
                    activeModifiers.Add(modifiers[j]);
            }
        }

        multiplierCache.Clear();
        Changed?.Invoke();
    }
}
