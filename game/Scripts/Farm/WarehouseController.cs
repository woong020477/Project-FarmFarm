// 역할: 운반물을 즉시 로컬 입고하고 장부/백업을 유지한 뒤 묶음으로 서버에 반영한다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Enum;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class WarehouseController : MonoBehaviour
{
    private const int JournalVersion = 2;
    private const long MaxBatchAmount = 64;
    [Serializable]
    private sealed class DepositJournal
    {
        public int version = JournalVersion;
        public List<DepositBatch> batches = new();
    }

    [Serializable]
    private sealed class DepositBatch
    {
        public string batchId;
        public List<DepositEntry> entries = new();
        public long TotalAmount
        {
            get
            {
                long total = 0;
                foreach (DepositEntry entry in entries)
                    total += entry.amount;
                return total;
            }
        }
    }

    [Serializable]
    private sealed class DepositEntry
    {
        public string harvestId;
        public CropType cropType;
        public long amount;
        public DepositEntry(string harvestId, CropType cropType, long amount)
        {
            this.harvestId = harvestId;
            this.cropType = cropType;
            this.amount = amount;
        }
    }

    [Serializable]
    private sealed class CropStock
    {
        [SerializeField]
        private CropType cropType;
        [SerializeField]
        private long quantity;
        public CropType CropType => cropType;
        public long Quantity => quantity;

        public CropStock(CropType cropType)
        {
            this.cropType = cropType;
            quantity = 0;
        }

        public void Set(long amount)
        {
            quantity = Math.Max(0, amount);
        }
    }

    [Header("Runtime Stock")]
    [SerializeField]
    private List<CropStock> cropStocks = new();
    [Header("Navigation")]
    [SerializeField]
    private Transform depositPoint;
    private PlayFabInventoryService inventoryService => GameManager.Instance != null ? GameManager.Instance.Inventory : null;

    [Header("Server")]
    [Min(0.1f)]
    [SerializeField]
    private float depositBatchDelaySeconds = 10f;
    [Min(1f)]
    [SerializeField]
    private float depositRetryDelaySeconds = 10f;
    [Min(1f)]
    [SerializeField]
    private float maxDepositRetryDelaySeconds = 60f;
    private readonly Dictionary<CropType, CropStock> stockByType = new();
    private readonly Dictionary<CropType, long> pendingAmountByType = new();
    private readonly HashSet<string> readyBatchIds = new();
    private PlayFabBootstrap bootstrap;
    private DepositJournal journal = new();
    private DepositBatch collectingBatch;
    private Coroutine collectionRoutine;
    private Coroutine submissionRoutine;
    private bool isJournalInitialized;
    private bool isSubmittingBatch;
    private float currentRetryDelaySeconds;
    private string journalPath;
    public Vector3 DepositPosition => depositPoint == null ? transform.position : depositPoint.position;

    public event Action StockChanged;
    private void Awake()
    {
        currentRetryDelaySeconds = depositRetryDelaySeconds;
        CacheStocks();
    }

    private void Start()
    {
        bootstrap = PlayFabBootstrap.Instance;
        if (bootstrap == null)
        {
            Debug.LogError("WarehouseController가 PlayFabBootstrap을 찾을 수 없습니다.");
            return;
        }

        if (bootstrap.IsReady)
            InitializeJournal();
        else
            bootstrap.Ready += InitializeJournal;
    }

    private void OnDestroy()
    {
        if (bootstrap != null)
            bootstrap.Ready -= InitializeJournal;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        CropCarrier carrier = player == null ? null : player.CropCarrier;
        if (carrier == null)
        {
            DroneController drone = other.GetComponentInParent<DroneController>();
            carrier = drone == null ? null : drone.CropCarrier;
        }

        TryDeposit(carrier);
    }

    public bool TryDeposit(CropCarrier carrier)
    {
        if (carrier == null || !carrier.IsCarrying)
            return false;
        if (inventoryService == null)
        {
            Debug.LogError("WarehouseController에 PlayFabInventoryService가 연결되지 않았습니다.");
            return false;
        }

        if (!isJournalInitialized)
        {
            Debug.Log("PlayFab 로그인과 입고 장부 준비가 끝난 뒤 작물을 보관할 수 있습니다.");
            return false;
        }

        if (!carrier.TryGetDepositInfo(out CropDefinition crop, out long amount, out string harvestId) || amount < 1 || amount > MaxBatchAmount)
        {
            Debug.LogWarning("작물 입고 정보가 올바르지 않습니다.");
            return false;
        }

        if (!carrier.TryUnload(crop, amount))
            return false;
        string previousJournalJson = JsonUtility.ToJson(journal);
        string previousCollectingBatchId = collectingBatch?.batchId;
        HashSet<string> previousReadyBatchIds = new(readyBatchIds);
        AddPendingHarvest(harvestId, crop.CropType, amount);
        if (!SaveJournal())
        {
            RestoreRuntimeJournal(previousJournalJson, previousCollectingBatchId, previousReadyBatchIds);
            EnsureCollectionTimer();
            carrier.RestoreState(crop, amount, harvestId);
            return false;
        }

        EnsureCollectionTimer();
        ScheduleSubmission(0.1f);
        StockChanged?.Invoke();
        Debug.Log($"{crop.CropType} 작물 {amount}개를 창고에 보관했습니다. 서버 전송 대기 수량: {GetPendingStock(crop.CropType)}개");
        return true;
    }

    public long GetStock(CropType cropType)
    {
        long confirmed = GetConfirmedStock(cropType);
        long pending = GetPendingStock(cropType);
        return confirmed > long.MaxValue - pending ? long.MaxValue : confirmed + pending;
    }

    public long GetConfirmedStock(CropType cropType)
    {
        return stockByType.TryGetValue(cropType, out CropStock stock) ? stock.Quantity : 0;
    }

    public long GetPendingStock(CropType cropType)
    {
        return pendingAmountByType.TryGetValue(cropType, out long amount) ? amount : 0;
    }

    public bool ContainsPendingHarvest(string harvestId)
    {
        if (string.IsNullOrWhiteSpace(harvestId))
            return false;
        foreach (DepositBatch batch in journal.batches)
        {
            if (batch.entries.Exists(entry => entry.harvestId == harvestId))
                return true;
        }

        return false;
    }

    public void SetStock(CropType cropType, long amount)
    {
        GetOrCreateStock(cropType).Set(amount);
    }

    private void InitializeJournal()
    {
        bootstrap.Ready -= InitializeJournal;
        journalPath = Path.Combine(Application.persistentDataPath, $"FarmFarm.PendingDeposits.{bootstrap.PlayFabId}.json");
        LoadJournal();
        isJournalInitialized = true;
        foreach (DepositBatch batch in journal.batches)
            readyBatchIds.Add(batch.batchId);
        RebuildPendingAmounts();
        ClearAlreadyDepositedCarriers();
        StockChanged?.Invoke();
        ScheduleSubmission(0.1f);
    }

    private void ClearAlreadyDepositedCarriers()
    {
        if (GameManager.Instance == null)
            return;
        foreach (IFarmAgent agent in GameManager.Instance.GetFarmAgents())
        {
            CropCarrier carrier = agent.CropCarrier;
            if (carrier != null && ContainsPendingHarvest(carrier.HarvestId))
                carrier.RestoreState(null, 0, null);
        }
    }

    private void AddPendingHarvest(string harvestId, CropType cropType, long amount)
    {
        if (collectingBatch == null || collectingBatch.TotalAmount > MaxBatchAmount - amount)
        {
            if (collectingBatch != null)
            {
                readyBatchIds.Add(collectingBatch.batchId);
                if (collectionRoutine != null)
                {
                    StopCoroutine(collectionRoutine);
                    collectionRoutine = null;
                }
            }

            collectingBatch = new DepositBatch
            {
                batchId = Guid.NewGuid().ToString("N")
            };
            journal.batches.Add(collectingBatch);
        }

        collectingBatch.entries.Add(new DepositEntry(harvestId, cropType, amount));
        AddPendingTotal(cropType, amount);
        if (collectingBatch.TotalAmount >= MaxBatchAmount)
        {
            readyBatchIds.Add(collectingBatch.batchId);
            collectingBatch = null;
            if (collectionRoutine != null)
            {
                StopCoroutine(collectionRoutine);
                collectionRoutine = null;
            }
        }
    }

    private void EnsureCollectionTimer()
    {
        if (collectingBatch != null && collectionRoutine == null)
            collectionRoutine = StartCoroutine(SealBatchAfterDelay(collectingBatch.batchId));
    }

    private IEnumerator SealBatchAfterDelay(string batchId)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, depositBatchDelaySeconds));
        collectionRoutine = null;
        if (collectingBatch == null || collectingBatch.batchId != batchId)
            yield break;
        readyBatchIds.Add(batchId);
        collectingBatch = null;
        SaveJournal();
        ScheduleSubmission(0.1f);
    }

    private void ScheduleSubmission(float delaySeconds)
    {
        if (submissionRoutine == null && !isSubmittingBatch && readyBatchIds.Count > 0)
            submissionRoutine = StartCoroutine(WaitAndSubmitBatch(delaySeconds));
    }

    private IEnumerator WaitAndSubmitBatch(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, delaySeconds));
        submissionRoutine = null;
        SubmitNextBatch();
    }

    private void SubmitNextBatch()
    {
        if (isSubmittingBatch || readyBatchIds.Count == 0)
            return;
        if (inventoryService == null || !inventoryService.CanSubmitInventoryMutation)
        {
            ScheduleSubmission(1f);
            return;
        }

        DepositBatch batch = journal.batches.Find(candidate => readyBatchIds.Contains(candidate.batchId));
        if (batch == null)
        {
            readyBatchIds.Clear();
            return;
        }

        List<CropDepositRequest> entries = new();
        foreach (DepositEntry entry in batch.entries)
            entries.Add(new CropDepositRequest(entry.harvestId, entry.cropType, entry.amount));
        isSubmittingBatch = true;
        inventoryService.DepositHarvestBatch(batch.batchId, entries, result =>
        {
            isSubmittingBatch = false;
            currentRetryDelaySeconds = depositRetryDelaySeconds;
            journal.batches.Remove(batch);
            readyBatchIds.Remove(batch.batchId);
            foreach (DepositEntry entry in batch.entries)
                AddPendingTotal(entry.cropType, -entry.amount);
            SaveJournal();
            ApplyConfirmedStocks(result.ConfirmedTotals);
            StockChanged?.Invoke();
            Debug.Log($"작물 {result.TotalAmount}개 서버 일괄 입고 완료{(result.WasDuplicate ? " (중복 요청 확인)" : string.Empty)}");
            FarmSaveService.Instance?.RequestCriticalSave();
            ScheduleSubmission(0.1f);
        }, error =>
        {
            isSubmittingBatch = false;
            Debug.LogError($"작물 서버 일괄 입고 실패, 로컬 장부를 유지하고 재시도합니다.\n{error}");
            float retryDelay = Mathf.Max(1f, currentRetryDelaySeconds);
            currentRetryDelaySeconds = Mathf.Min(Mathf.Max(1f, maxDepositRetryDelaySeconds), retryDelay * 2f);
            ScheduleSubmission(retryDelay);
        });
    }

    private void ApplyConfirmedStocks(IReadOnlyDictionary<CropType, long> confirmedTotals)
    {
        foreach (CropType cropType in System.Enum.GetValues(typeof(CropType)))
        {
            long amount = confirmedTotals != null && confirmedTotals.TryGetValue(cropType, out long confirmedAmount) ? confirmedAmount : 0;
            GetOrCreateStock(cropType).Set(amount);
        }
    }

    private void CacheStocks()
    {
        stockByType.Clear();
        foreach (CropStock stock in cropStocks)
        {
            if (stock == null || stockByType.ContainsKey(stock.CropType))
                continue;
            stockByType.Add(stock.CropType, stock);
        }

        foreach (CropType cropType in System.Enum.GetValues(typeof(CropType)))
            GetOrCreateStock(cropType);
    }

    private CropStock GetOrCreateStock(CropType cropType)
    {
        if (stockByType.TryGetValue(cropType, out CropStock stock))
            return stock;
        stock = new CropStock(cropType);
        cropStocks.Add(stock);
        stockByType.Add(cropType, stock);
        return stock;
    }

    private void AddPendingTotal(CropType cropType, long amount)
    {
        pendingAmountByType.TryGetValue(cropType, out long currentAmount);
        long nextAmount = Math.Max(0, currentAmount + amount);
        if (nextAmount == 0)
            pendingAmountByType.Remove(cropType);
        else
            pendingAmountByType[cropType] = nextAmount;
    }

    private void RebuildPendingAmounts()
    {
        pendingAmountByType.Clear();
        foreach (DepositBatch batch in journal.batches)
        {
            foreach (DepositEntry entry in batch.entries)
                AddPendingTotal(entry.cropType, entry.amount);
        }
    }

    private bool SaveJournal()
    {
        if (string.IsNullOrWhiteSpace(journalPath))
            return false;
        string temporaryPath = journalPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(journal));
            if (File.Exists(journalPath))
                File.Replace(temporaryPath, journalPath, journalPath + ".bak");
            else
                File.Move(temporaryPath, journalPath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"작물 입고 장부 저장 실패: {exception.Message}");
            return false;
        }
    }

    private void LoadJournal()
    {
        journal = new DepositJournal();
        string backupPath = journalPath + ".bak";
        if (!File.Exists(journalPath) && !File.Exists(backupPath))
            return;
        if (TryLoadJournalFile(journalPath, out DepositJournal loadedJournal, out string primaryError, out bool primaryRequiresRewrite))
        {
            journal = loadedJournal;
            if (primaryRequiresRewrite)
                SaveJournal();
            return;
        }

        if (File.Exists(journalPath))
        {
            string corruptPath = journalPath + $".corrupt.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            try
            {
                File.Move(journalPath, corruptPath);
            }
            catch (Exception moveException)
            {
                Debug.LogError($"손상된 입고 장부 보존 실패: {moveException.Message}");
            }
        }

        if (TryLoadJournalFile(backupPath, out DepositJournal backupJournal, out string backupError, out _))
        {
            journal = backupJournal;
            Debug.LogWarning($"기본 작물 입고 장부가 손상되어 백업 장부를 복구했습니다: {primaryError}");
            SaveJournal();
            return;
        }

        if (SaveJournal())
            Debug.LogWarning($"기존 작물 입고 장부를 읽지 못해 보존한 뒤 새 장부를 생성했습니다. 기본 장부 오류: {primaryError}, 백업 오류: {backupError}");
        else
            Debug.LogError($"작물 입고 장부와 백업 장부를 읽지 못했고 새 장부도 생성하지 못했습니다. 기본 장부 오류: {primaryError}, 백업 오류: {backupError}");
    }

    private static bool TryLoadJournalFile(string path, out DepositJournal loadedJournal, out string error, out bool requiresRewrite)
    {
        loadedJournal = null;
        error = string.Empty;
        requiresRewrite = false;
        if (!File.Exists(path))
        {
            error = "파일 없음";
            return false;
        }

        try
        {
            loadedJournal = JsonUtility.FromJson<DepositJournal>(File.ReadAllText(path));
            if (loadedJournal != null && loadedJournal.version == 1 && loadedJournal.batches != null && loadedJournal.batches.Count == 0)
            {
                loadedJournal.version = JournalVersion;
                requiresRewrite = true;
            }

            ValidateJournal(loadedJournal);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void ValidateJournal(DepositJournal targetJournal)
    {
        if (targetJournal == null || targetJournal.version != JournalVersion || targetJournal.batches == null)
            throw new InvalidDataException("지원하지 않는 입고 장부 형식입니다.");
        HashSet<string> batchIds = new();
        foreach (DepositBatch batch in targetJournal.batches)
        {
            if (batch == null || !Guid.TryParseExact(batch.batchId, "N", out _) || !batchIds.Add(batch.batchId) || batch.entries == null || batch.entries.Count == 0)
                throw new InvalidDataException("입고 배치 정보가 올바르지 않습니다.");
            long totalAmount = 0;
            foreach (DepositEntry entry in batch.entries)
            {
                if (entry == null || !Guid.TryParseExact(entry.harvestId, "N", out _) || !System.Enum.IsDefined(typeof(CropType), entry.cropType) || entry.amount < 1 || totalAmount > MaxBatchAmount - entry.amount)
                    throw new InvalidDataException("입고 작물 정보가 올바르지 않습니다.");
                totalAmount += entry.amount;
            }
        }
    }

    private void RestoreRuntimeJournal(string journalJson, string collectingBatchId, HashSet<string> previousReadyBatchIds)
    {
        journal = JsonUtility.FromJson<DepositJournal>(journalJson) ?? new DepositJournal();
        readyBatchIds.Clear();
        foreach (string batchId in previousReadyBatchIds)
            readyBatchIds.Add(batchId);
        collectingBatch = string.IsNullOrWhiteSpace(collectingBatchId) ? null : journal.batches.Find(batch => batch.batchId == collectingBatchId);
        RebuildPendingAmounts();
    }
}
