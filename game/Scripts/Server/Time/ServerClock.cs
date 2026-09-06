// 역할: 주기적으로 받은 서버 시간과 로컬 경과 시간을 조합해 현재 서버 시각을 추정한다.
using System;
using System.Collections;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public sealed class ServerClock : MonoBehaviour
{
    public static ServerClock Instance { get; private set; }

    [Min(10f)]
    [SerializeField]
    private float syncIntervalSeconds = 60f;
    private PlayFabBootstrap bootstrap;
    private Coroutine syncRoutine;
    private DateTime syncedUtcTime;
    private double syncedRealtime;
    private bool isSyncing;
    public bool IsReady { get; private set; }
    public bool IsConnectionAvailable { get; private set; }
    public DateTime UtcNow => IsReady ? syncedUtcTime.AddSeconds(Time.realtimeSinceStartupAsDouble - syncedRealtime) : DateTime.UtcNow;
    public long UnixTimeSeconds => new DateTimeOffset(DateTime.SpecifyKind(UtcNow, DateTimeKind.Utc)).ToUnixTimeSeconds();

    public event Action Synced;
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
        bootstrap = PlayFabBootstrap.Instance;
        if (bootstrap == null)
        {
            Debug.LogError("ServerClock이 PlayFabBootstrap을 찾을 수 없습니다.");
            return;
        }

        if (bootstrap.IsReady)
            BeginSync();
        else
            bootstrap.Ready += BeginSync;
    }

    private void OnDestroy()
    {
        if (bootstrap != null)
            bootstrap.Ready -= BeginSync;
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            SyncNow();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
            SyncNow();
    }

    public void SyncNow()
    {
        if (isSyncing || bootstrap == null || !bootstrap.IsReady)
            return;
        isSyncing = true;
        PlayFabClientAPI.GetTime(new GetTimeRequest(), OnTimeLoaded, OnTimeFailed);
    }

    private void BeginSync()
    {
        if (syncRoutine != null)
            return;
        syncRoutine = StartCoroutine(SyncLoop());
    }

    private IEnumerator SyncLoop()
    {
        while (true)
        {
            SyncNow();
            yield return new WaitForSecondsRealtime(Mathf.Max(10f, syncIntervalSeconds));
        }
    }

    private void OnTimeLoaded(GetTimeResult result)
    {
        isSyncing = false;
        syncedUtcTime = DateTime.SpecifyKind(result.Time, DateTimeKind.Utc);
        syncedRealtime = Time.realtimeSinceStartupAsDouble;
        IsReady = true;
        IsConnectionAvailable = true;
        Synced?.Invoke();
    }

    private void OnTimeFailed(PlayFabError error)
    {
        isSyncing = false;
        IsConnectionAvailable = false;
        Debug.LogError($"서버 시간 동기화 실패\n{error.GenerateErrorReport()}");
    }
}
