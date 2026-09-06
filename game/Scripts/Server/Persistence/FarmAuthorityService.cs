// 역할: 농지 상태 동기화와 서버 응답 처리를 분리한 통신 경계.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FarmAuthorityService : MonoBehaviour
{
    private sealed class PendingCommand
    {
        public FarmCommandData Data { get; }
        public Action<FarmCommandResultData> Completed { get; }
        public int Attempts { get; set; }

        public PendingCommand(FarmCommandData data, Action<FarmCommandResultData> completed)
        {
            Data = data;
            Completed = completed;
        }
    }

    public static FarmAuthorityService Instance { get; private set; }

    [Header("Request Batching")]
    [Min(0.05f)]
    [SerializeField]
    private float batchDelaySeconds = 0.25f;
    [Range(1, 64)]
    [SerializeField]
    private int maxCommandsPerBatch = 64;
    private readonly List<PendingCommand> pendingCommands = new();
    private PlayFabBootstrap bootstrap;
    private Coroutine batchRoutine;
    private bool isSubmitting;
    private bool isSynchronized;
    public bool IsReady => bootstrap != null && bootstrap.IsReady && isSynchronized;

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
            Debug.LogError("FarmAuthorityService가 PlayFabBootstrap을 찾을 수 없습니다.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool Enqueue(FarmPlotController farmPlot, FarmWorkType workType, Vector3Int position, int expectedRevision, string agentId, Action<FarmCommandResultData> completed)
    {
        if (farmPlot == null || !IsReady || pendingCommands.Count >= 256)
            return false;
        FarmCommandData command = new()
        {
            commandId = Guid.NewGuid().ToString("N"),
            action = workType.ToString().ToLowerInvariant(),
            farmId = farmPlot.PersistentId,
            positionX = position.x,
            positionY = position.y,
            positionZ = position.z,
            expectedRevision = expectedRevision,
            cropType = farmPlot.ActiveCropType,
            agentId = agentId
        };
        pendingCommands.Add(new PendingCommand(command, completed));
        if (pendingCommands.Count >= maxCommandsPerBatch)
            SubmitNextBatch();
        else if (batchRoutine == null)
            batchRoutine = StartCoroutine(SubmitAfterDelay());
        return true;
    }

    public void Synchronize(IReadOnlyList<FarmPlotController> farmPlots, IReadOnlyList<IFarmAgent> agents, Action completed)
    {
        bootstrap ??= PlayFabBootstrap.Instance;
        if (bootstrap == null || !bootstrap.IsReady)
        {
            completed?.Invoke();
            return;
        }

        List<string> plotIds = new();
        if (farmPlots != null)
        {
            foreach (FarmPlotController farmPlot in farmPlots)
            {
                if (farmPlot != null && !string.IsNullOrWhiteSpace(farmPlot.PersistentId))
                    plotIds.Add(farmPlot.PersistentId);
            }
        }

        ExecuteCloudScriptRequest request = new()
        {
            FunctionName = "loadFarmAuthority",
            FunctionParameter = new
            {
                plotIds
            },
            RevisionSelection = CloudScriptRevisionOption.Live
        };
        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            if (result.Error != null)
            {
                Debug.LogError($"서버 농지 동기화 실패\n{BuildCloudScriptErrorReport(result)}");
                completed?.Invoke();
                return;
            }

            if (!TryReadState(result.FunctionResult, out FarmAuthorityState state))
            {
                Debug.LogError("서버 농지 동기화 응답을 읽을 수 없습니다.");
                completed?.Invoke();
                return;
            }

            ApplyState(state, farmPlots, agents);
            isSynchronized = true;
            Debug.Log("서버 권위 농지 상태 동기화 성공");
            completed?.Invoke();
        }, error =>
        {
            Debug.LogError($"서버 농지 동기화 실패\n{error.GenerateErrorReport()}");
            completed?.Invoke();
        });
    }

    private IEnumerator SubmitAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, batchDelaySeconds));
        batchRoutine = null;
        SubmitNextBatch();
    }

    private void SubmitNextBatch()
    {
        if (isSubmitting || pendingCommands.Count == 0 || !IsReady)
            return;
        int count = Mathf.Min(maxCommandsPerBatch, pendingCommands.Count);
        List<PendingCommand> submitted = pendingCommands.GetRange(0, count);
        pendingCommands.RemoveRange(0, count);
        FarmCommandBatchData batch = new();
        foreach (PendingCommand command in submitted)
        {
            command.Attempts++;
            batch.commands.Add(command.Data);
        }

        isSubmitting = true;
        ExecuteCloudScriptRequest request = new()
        {
            FunctionName = "applyFarmCommands",
            FunctionParameter = batch,
            RevisionSelection = CloudScriptRevisionOption.Live
        };
        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            isSubmitting = false;
            if (result.Error != null)
            {
                CompleteAsFailed(submitted, BuildCloudScriptErrorReport(result));
                ScheduleRemaining();
                return;
            }

            if (!TryReadResults(result.FunctionResult, out FarmCommandResultList resultList))
            {
                RetryOrFail(submitted, "농사 명령 응답 형식이 올바르지 않습니다.");
                return;
            }

            Dictionary<string, FarmCommandResultData> resultById = new();
            foreach (FarmCommandResultData commandResult in resultList.results)
                resultById[commandResult.commandId] = commandResult;
            foreach (PendingCommand command in submitted)
            {
                if (resultById.TryGetValue(command.Data.commandId, out FarmCommandResultData commandResult))
                    command.Completed?.Invoke(commandResult);
                else
                    command.Completed?.Invoke(CreateFailure(command.Data, "서버 응답에서 명령 결과를 찾지 못했습니다."));
            }

            ScheduleRemaining();
        }, error =>
        {
            isSubmitting = false;
            RetryOrFail(submitted, error.GenerateErrorReport());
        });
    }

    private void RetryOrFail(List<PendingCommand> commands, string error)
    {
        bool canRetry = commands.TrueForAll(command => command.Attempts < 3);
        if (!canRetry)
        {
            CompleteAsFailed(commands, error);
            ScheduleRemaining();
            return;
        }

        pendingCommands.InsertRange(0, commands);
        ScheduleRemaining();
    }

    private void ScheduleRemaining()
    {
        if (pendingCommands.Count > 0 && batchRoutine == null)
            batchRoutine = StartCoroutine(SubmitAfterDelay());
    }

    private static void CompleteAsFailed(IEnumerable<PendingCommand> commands, string error)
    {
        foreach (PendingCommand command in commands)
            command.Completed?.Invoke(CreateFailure(command.Data, error));
    }

    private static string BuildCloudScriptErrorReport(ExecuteCloudScriptResult result)
    {
        StringBuilder report = new();
        if (result?.Error != null)
        {
            report.Append(result.Error.Error).Append(": ").Append(result.Error.Message);
            if (!string.IsNullOrWhiteSpace(result.Error.StackTrace))
                report.AppendLine().Append(result.Error.StackTrace);
        }

        if (result?.Logs != null)
        {
            foreach (LogStatement log in result.Logs)
            {
                if (log == null)
                    continue;
                report.AppendLine().Append('[').Append(log.Level).Append("] ").Append(log.Message);
            }
        }

        return report.Length == 0 ? "CloudScript 오류 정보가 없습니다." : report.ToString();
    }

    private static FarmCommandResultData CreateFailure(FarmCommandData command, string error)
    {
        return new FarmCommandResultData
        {
            commandId = command.commandId,
            farmId = command.farmId,
            positionX = command.positionX,
            positionY = command.positionY,
            positionZ = command.positionZ,
            success = false,
            error = error
        };
    }

    private static bool TryReadResults(object functionResult, out FarmCommandResultList results)
    {
        results = null;
        if (functionResult is not IDictionary<string, object> data || !data.TryGetValue("resultsJson", out object value))
            return false;
        results = JsonUtility.FromJson<FarmCommandResultList>(Convert.ToString(value));
        return results?.results != null;
    }

    private static bool TryReadState(object functionResult, out FarmAuthorityState state)
    {
        state = null;
        if (functionResult is not IDictionary<string, object> data || !data.TryGetValue("stateJson", out object value))
            return false;
        state = JsonUtility.FromJson<FarmAuthorityState>(Convert.ToString(value));
        return state != null;
    }

    private static void ApplyState(FarmAuthorityState state, IReadOnlyList<FarmPlotController> farmPlots, IReadOnlyList<IFarmAgent> agents)
    {
        Dictionary<string, FarmPlotController> plotById = new();
        if (farmPlots != null)
        {
            foreach (FarmPlotController farmPlot in farmPlots)
            {
                if (farmPlot != null)
                    plotById[farmPlot.PersistentId] = farmPlot;
            }
        }

        foreach (FarmAuthorityPlotState plotState in state.plots)
        {
            if (plotState != null && plotById.TryGetValue(plotState.persistentId, out FarmPlotController farmPlot))
                farmPlot.RestoreAuthoritativeCells(plotState.cells);
        }

        Dictionary<string, IFarmAgent> agentById = new();
        if (agents != null)
        {
            foreach (IFarmAgent agent in agents)
            {
                agentById[agent.PersistentId] = agent;
                if (agent.CropCarrier != null)
                    agent.CropCarrier.RestoreState(null, 0, null);
            }
        }

        foreach (FarmAuthorityCargoState cargo in state.cargos)
        {
            if (cargo == null || !agentById.TryGetValue(cargo.agentId, out IFarmAgent agent) || agent.CropCarrier == null)
                continue;
            if (GameManager.Instance != null && GameManager.Instance.IsHarvestPendingDeposit(cargo.harvestId))
                continue;
            if (GameManager.Instance != null && GameManager.Instance.TryGetCropDefinition(cargo.cropType, out CropDefinition crop))
                agent.CropCarrier.RestoreState(crop, cargo.amount, cargo.harvestId);
        }
    }
}
