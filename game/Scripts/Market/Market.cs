// 역할: 서버 시계를 기준으로 헬기 체류와 판매 가능 상태를 계산한다.
using System;
using System.Collections;
using System.Collections.Generic;
using Enum;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public sealed class Market : MonoBehaviour
{
    private const int OpenDurationSeconds = 300;
    private const int ClosedDurationSeconds = 300;
    private const int CycleDurationSeconds = OpenDurationSeconds + ClosedDurationSeconds;
    private ServerClock clock => GameManager.Instance != null ? GameManager.Instance.Clock : ServerClock.Instance;
    private GameEvents gameEvents => GameManager.Instance != null ? GameManager.Instance.Events : GameEvents.Instance;
    private GameObject helicopterObject => GameManager.Instance != null ? GameManager.Instance.Helicopter : null;

    private readonly Dictionary<CropType, int> priceByCrop = new();
    private PlayFabBootstrap bootstrap;
    private bool isLoading;
    private bool observedOpen;
    private int observedRemainingSeconds = -1;
    private float nextScheduleUpdateTime;
    public bool IsReady { get; private set; }
    public bool IsOpen { get; private set; }
    public int RemainingSeconds { get; private set; }

    public event Action StateChanged;
    private void Start()
    {
        bootstrap = PlayFabBootstrap.Instance;
        if (clock != null)
            clock.Synced += RefreshState;
        if (bootstrap == null)
        {
            Debug.LogError("Market이 PlayFabBootstrap을 찾을 수 없습니다.");
            SetHelicopterVisible(false);
            return;
        }

        if (bootstrap.IsReady)
            RefreshState();
        else
            bootstrap.Ready += RefreshState;
        EvaluateSchedule();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScheduleUpdateTime)
            return;
        nextScheduleUpdateTime = Time.unscaledTime + 0.25f;
        EvaluateSchedule();
    }

    private void OnDestroy()
    {
        if (clock != null)
            clock.Synced -= RefreshState;
        if (bootstrap != null)
            bootstrap.Ready -= RefreshState;
    }

    public int GetPrice(CropType cropType)
    {
        if (priceByCrop.TryGetValue(cropType, out int price))
            return price;
        // Authoring preview only. Trading still requires IsReady and a server price.
        return !IsReady && GameManager.Instance != null && GameManager.Instance.TryGetCropDefinition(cropType, out var definition) ? definition.BasePrice : 0;
    }

    public void RefreshState()
    {
        if (isLoading || bootstrap == null || !bootstrap.IsReady)
            return;
        isLoading = true;
        ExecuteCloudScriptRequest request = new()
        {
            FunctionName = "getMarketState",
            RevisionSelection = CloudScriptRevisionOption.Live
        };
        PlayFabClientAPI.ExecuteCloudScript(request, OnStateLoaded, OnStateFailed);
    }

    private void EvaluateSchedule()
    {
        if (clock == null || !clock.IsReady)
        {
            ApplyOpenState(false, 0);
            return;
        }

        long cyclePosition = PositiveModulo(clock.UnixTimeSeconds, CycleDurationSeconds);
        bool open = cyclePosition < OpenDurationSeconds;
        int remaining = open ? OpenDurationSeconds - (int)cyclePosition : CycleDurationSeconds - (int)cyclePosition;
        ApplyOpenState(open, remaining);
    }

    private void OnStateLoaded(ExecuteCloudScriptResult result)
    {
        isLoading = false;
        if (result.Error != null)
        {
            Debug.LogError($"시장 상태 조회 실패: {result.Error.Error}: {result.Error.Message}");
            return;
        }

        if (!TryGetDictionary(result.FunctionResult, out IDictionary<string, object> data))
        {
            Debug.LogError("시장 상태 응답 형식이 올바르지 않습니다.");
            return;
        }

        ReadPrices(data);
        ReadServerEvent(data);
        IsReady = true;
        EvaluateSchedule();
        StateChanged?.Invoke();
    }

    private void OnStateFailed(PlayFabError error)
    {
        isLoading = false;
        Debug.LogError($"시장 상태 조회 실패\n{error.GenerateErrorReport()}");
    }

    private void ReadPrices(IDictionary<string, object> data)
    {
        if (!data.TryGetValue("prices", out object pricesValue) || !TryGetDictionary(pricesValue, out IDictionary<string, object> prices))
            return;
        priceByCrop.Clear();
        if (GameManager.Instance == null)
            return;
        foreach (var definition in GameManager.Instance.CropDefinitions)
            if (definition != null)
                SetPrice(prices, definition.ServerId, definition.CropType);
    }

    private void ReadServerEvent(IDictionary<string, object> data)
    {
        if (gameEvents == null)
            return;
        if (!data.TryGetValue("activeEvent", out object eventValue) || eventValue == null || !TryGetDictionary(eventValue, out IDictionary<string, object> eventData))
        {
            gameEvents.ClearServerEvent();
            return;
        }

        string eventId = GetString(eventData, "id");
        string title = GetString(eventData, "title");
        string description = GetString(eventData, "description");
        long endUnixSeconds = GetLong(eventData, "endUnixSeconds");
        List<StatModifier> modifiers = ReadModifiers(eventData);
        gameEvents.ApplyServerEvent(eventId, title, description, endUnixSeconds, modifiers);
    }

    private static List<StatModifier> ReadModifiers(IDictionary<string, object> eventData)
    {
        List<StatModifier> modifiers = new();
        if (!eventData.TryGetValue("modifiers", out object modifierValue) || modifierValue is not IList modifierList)
            return modifiers;
        foreach (object entry in modifierList)
        {
            if (!TryGetDictionary(entry, out IDictionary<string, object> modifierData))
                continue;
            string statName = GetString(modifierData, "stat");
            if (!System.Enum.TryParse(statName, out StatType stat))
                continue;
            string targetId = GetString(modifierData, "targetId");
            float percent = Convert.ToSingle(GetDouble(modifierData, "percent"));
            modifiers.Add(new StatModifier(stat, targetId, percent));
        }

        return modifiers;
    }

    private void SetPrice(IDictionary<string, object> prices, string itemId, CropType cropType)
    {
        if (!prices.TryGetValue(itemId, out object value))
            return;
        int price = Mathf.Max(1, Convert.ToInt32(value));
        priceByCrop[cropType] = price;
    }

    private void ApplyOpenState(bool open, int remainingSeconds)
    {
        IsOpen = open;
        RemainingSeconds = Mathf.Max(0, remainingSeconds);
        SetHelicopterVisible(open);
        if (observedOpen == IsOpen && observedRemainingSeconds == RemainingSeconds)
            return;
        bool openChanged = observedOpen != IsOpen;
        observedOpen = IsOpen;
        observedRemainingSeconds = RemainingSeconds;
        if (openChanged)
            StateChanged?.Invoke();
    }

    private void SetHelicopterVisible(bool visible)
    {
        if (helicopterObject != null && helicopterObject.activeSelf != visible)
            helicopterObject.SetActive(visible);
    }

    private static long PositiveModulo(long value, long divisor)
    {
        long remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }

    private static bool TryGetDictionary(object value, out IDictionary<string, object> dictionary)
    {
        dictionary = value as IDictionary<string, object>;
        return dictionary != null;
    }

    private static string GetString(IDictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out object value) && value != null ? Convert.ToString(value) : string.Empty;
    }

    private static long GetLong(IDictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out object value) && value != null ? Convert.ToInt64(value) : 0L;
    }

    private static double GetDouble(IDictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out object value) && value != null ? Convert.ToDouble(value) : 0d;
    }
}
