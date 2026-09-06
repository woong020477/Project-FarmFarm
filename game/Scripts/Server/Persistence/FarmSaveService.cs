// 역할: 정기/중요 시점 농장 스냅샷 저장과 복구를 조정한다.
using System;
using System.Collections;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FarmSaveService : MonoBehaviour
{
    public static FarmSaveService Instance { get; private set; }
    private GameManager gameManager => GameManager.Instance;
    private ServerClock serverClock => gameManager != null ? gameManager.Clock : ServerClock.Instance;

    [Header("Save Timing")]
    [Min(30f)]
    [SerializeField]
    private float periodicSaveIntervalSeconds = 200f;
    [Min(1f)]
    [SerializeField]
    private float criticalSaveMergeSeconds = 5f;
    [Min(0.5f)]
    [SerializeField]
    private float quitSaveTimeoutSeconds = 2f;
    private PlayFabBootstrap bootstrap;
    private Coroutine periodicSaveRoutine;
    private Coroutine criticalSaveRoutine;
    private bool isSaving;
    private bool criticalSavePending;
#if !UNITY_EDITOR
    private bool quitSaveStarted;
    private bool allowQuit;
#endif
    private float lastSaveRequestRealtime = float.NegativeInfinity;
    public bool IsLoaded { get; private set; }

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
            Debug.LogError("FarmSaveService가 PlayFabBootstrap을 찾을 수 없습니다.");
            return;
        }

        if (bootstrap.IsReady)
            BeginPersistence();
        else
            bootstrap.Ready += BeginPersistence;
    }

    private void OnEnable()
    {
        Application.wantsToQuit += HandleWantsToQuit;
    }

    private void OnDisable()
    {
        Application.wantsToQuit -= HandleWantsToQuit;
    }

    private void OnDestroy()
    {
        if (bootstrap != null)
            bootstrap.Ready -= BeginPersistence;
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            RequestCriticalSave();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Tab closure cannot wait for a coroutine. Start a best-effort save earlier,
        // when the tab loses focus; RequestCriticalSave already coalesces requests.
        if(!hasFocus)RequestCriticalSave();
#endif
    }

    private bool HandleWantsToQuit()
    {
#if UNITY_EDITOR || UNITY_WEBGL
        return true;
#else
        if (allowQuit || bootstrap == null || !bootstrap.IsReady)
            return true;
        if (!quitSaveStarted)
        {
            quitSaveStarted = true;
            StartCoroutine(SaveBeforeQuit());
        }

        return false;
#endif
    }

    public void RequestCriticalSave()
    {
        if (bootstrap == null || !bootstrap.IsReady)
            return;
        criticalSavePending = true;
        if (criticalSaveRoutine == null)
            criticalSaveRoutine = StartCoroutine(ProcessCriticalSaves());
    }

    private void BeginPersistence()
    {
        Load();
        if (periodicSaveRoutine == null)
            periodicSaveRoutine = StartCoroutine(PeriodicSaveLoop());
    }

    private IEnumerator PeriodicSaveLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(30f, periodicSaveIntervalSeconds));
            BeginSave();
        }
    }

    private IEnumerator ProcessCriticalSaves()
    {
        while (criticalSavePending)
        {
            criticalSavePending = false;
            float waitSeconds = criticalSaveMergeSeconds - (Time.realtimeSinceStartup - lastSaveRequestRealtime);
            if (waitSeconds > 0f)
                yield return new WaitForSecondsRealtime(waitSeconds);
            while (isSaving)
                yield return null;
            BeginSave();
            while (isSaving)
                yield return null;
        }

        criticalSaveRoutine = null;
    }

#if !UNITY_EDITOR
    private IEnumerator SaveBeforeQuit()
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, quitSaveTimeoutSeconds);
        while (isSaving && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (Time.realtimeSinceStartup < deadline)
        {
            BeginSave();
            while (isSaving && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        allowQuit = true;
        Application.Quit();
    }

#endif
    private void BeginSave()
    {
        if (isSaving || gameManager == null || bootstrap == null || !bootstrap.IsReady)
            return;
        FarmGameSaveData saveData = CaptureState();
        string snapshotJson = JsonUtility.ToJson(saveData);
        isSaving = true;
        lastSaveRequestRealtime = Time.realtimeSinceStartup;
        ExecuteCloudScriptRequest request = new()
        {
            FunctionName = "saveFarmState",
            FunctionParameter = new
            {
                snapshotJson
            },
            RevisionSelection = CloudScriptRevisionOption.Live
        };
        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            isSaving = false;
            if (result.Error != null)
            {
                Debug.LogError($"농장 상태 저장 실패: {result.Error.Error}: {result.Error.Message}");
                return;
            }

            Debug.Log("농장 상태 저장 성공");
        }, error =>
        {
            isSaving = false;
            Debug.LogError($"농장 상태 저장 실패\n{error.GenerateErrorReport()}");
        });
    }

    private void Load()
    {
        ExecuteCloudScriptRequest request = new()
        {
            FunctionName = "loadFarmState",
            RevisionSelection = CloudScriptRevisionOption.Live
        };
        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            if (result.Error != null)
            {
                Debug.LogError($"농장 상태 불러오기 실패: {result.Error.Error}: {result.Error.Message}");
                return;
            }

            if (!TryReadSnapshotJson(result.FunctionResult, out string snapshotJson))
            {
                Debug.Log("저장된 농장 상태가 없어 현재 씬 상태로 시작합니다.");
                CompleteLoad();
                return;
            }

            FarmGameSaveData saveData = JsonUtility.FromJson<FarmGameSaveData>(snapshotJson);
            if (saveData == null || saveData.version != 1)
            {
                Debug.LogError("농장 저장 데이터 버전을 읽을 수 없습니다.");
                return;
            }

            RestoreState(saveData);
            CompleteLoad();
        }, error => Debug.LogError($"농장 상태 불러오기 실패\n{error.GenerateErrorReport()}"));
    }

    private FarmGameSaveData CaptureState()
    {
        FarmGameSaveData saveData = new()
        {
            savedAtUnixSeconds = serverClock != null && serverClock.IsReady ? serverClock.UnixTimeSeconds : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            unlockedFarmPlotCount = gameManager.UnlockedFarmPlotCount,
            ownedDroneCount = gameManager.OwnedDroneCount,
            playerLevel = gameManager.PlayerProfile == null ? 1L : gameManager.PlayerProfile.Level,
            currentExperience = gameManager.PlayerProfile == null ? 0L : gameManager.PlayerProfile.CurrentExperience,
            playerTitle = gameManager.PlayerProfile == null ? "없음" : gameManager.PlayerProfile.Title
        };
        foreach (IFarmAgent agent in gameManager.GetFarmAgents())
        {
            FarmAgentSaveData agentSaveData = new()
            {
                persistentId = agent.PersistentId,
                positionX = agent.Position.x,
                positionY = agent.Position.y,
                positionZ = agent.Position.z
            };
            if (agent.CropCarrier != null && agent.CropCarrier.TryGetCarriedCrop(out CropDefinition crop, out long amount))
            {
                agentSaveData.isCarrying = true;
                agentSaveData.carriedCropType = crop.CropType;
                agentSaveData.carriedAmount = amount;
                agentSaveData.carriedHarvestId = agent.CropCarrier.HarvestId;
            }

            saveData.agents.Add(agentSaveData);
        }

        for (int index = 0; index < gameManager.UnlockedFarmPlotCount && index < gameManager.FarmPlots.Count; index++)
            saveData.farmPlots.Add(gameManager.FarmPlots[index].CaptureSaveData());
        return saveData;
    }

    private void CompleteLoad()
    {
        IsLoaded = true;
        Debug.Log("농장 상태 불러오기 성공");
    }

    private void RestoreState(FarmGameSaveData saveData)
    {
        gameManager.RestoreOwnership(saveData.unlockedFarmPlotCount, saveData.ownedDroneCount);
        gameManager.PlayerProfile?.Restore(saveData.playerLevel, saveData.currentExperience, saveData.playerTitle);
        Dictionary<string, FarmPlotSaveData> farmPlotById = new();
        if (saveData.farmPlots != null)
        {
            foreach (FarmPlotSaveData farmPlotSaveData in saveData.farmPlots)
            {
                if (farmPlotSaveData != null && !string.IsNullOrWhiteSpace(farmPlotSaveData.persistentId))
                    farmPlotById[farmPlotSaveData.persistentId] = farmPlotSaveData;
            }
        }

        foreach (FarmPlotController farmPlot in gameManager.FarmPlots)
        {
            if (farmPlotById.TryGetValue(farmPlot.PersistentId, out FarmPlotSaveData farmPlotSaveData))
                farmPlot.RestoreSaveData(farmPlotSaveData);
        }

        Dictionary<string, FarmAgentSaveData> agentById = new();
        if (saveData.agents != null)
        {
            foreach (FarmAgentSaveData agentSaveData in saveData.agents)
            {
                if (agentSaveData != null && !string.IsNullOrWhiteSpace(agentSaveData.persistentId))
                    agentById[agentSaveData.persistentId] = agentSaveData;
            }
        }

        foreach (IFarmAgent agent in gameManager.GetFarmAgents())
        {
            if (!agentById.TryGetValue(agent.PersistentId, out FarmAgentSaveData agentSaveData))
                continue;
            agent.RestorePosition(new Vector3(agentSaveData.positionX, agentSaveData.positionY, agentSaveData.positionZ));
            if (agent.CropCarrier == null)
                continue;
            CropDefinition carriedCrop = null;
            if (agentSaveData.isCarrying)
                gameManager.TryGetCropDefinition(agentSaveData.carriedCropType, out carriedCrop);
            agent.CropCarrier.RestoreState(carriedCrop, agentSaveData.carriedAmount, agentSaveData.carriedHarvestId);
        }
    }

    private static bool TryReadSnapshotJson(object functionResult, out string snapshotJson)
    {
        snapshotJson = null;
        if (functionResult is not IDictionary<string, object> data || !data.TryGetValue("snapshotJson", out object snapshotValue) || snapshotValue == null)
            return false;
        snapshotJson = Convert.ToString(snapshotValue);
        return !string.IsNullOrWhiteSpace(snapshotJson);
    }
}
