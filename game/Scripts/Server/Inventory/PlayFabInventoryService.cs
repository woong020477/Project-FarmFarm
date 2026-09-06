// 역할: 재고/골드 조회와 입고 요청을 직렬화하고 확정된 서버 상태를 화면에 전달한다.
using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using Enum;

public sealed partial class PlayFabInventoryService : MonoBehaviour
{
    private const string GoldCurrencyCode = "GD";
    private WarehouseController warehouse => GameManager.Instance != null ? GameManager.Instance.Warehouse : null;
    public int Gold { get; private set; }
    private bool CanContactInventory => bootstrap != null && bootstrap.IsReady && !isInventoryMutationInProgress && !isInventoryLoadInProgress;
    public bool CanSubmitInventoryMutation => CanContactInventory && pendingSale == null;

    public event Action InventoryUpdated;
    private PlayFabBootstrap bootstrap;
    private bool isInventoryMutationInProgress;
    private bool isInventoryLoadInProgress;
    private bool inventoryReloadPending;
    public bool TryBeginSharedMutation()
    {
        if (!CanSubmitInventoryMutation)
            return false;
        isInventoryMutationInProgress = true;
        return true;
    }

    public void EndSharedMutation() => EndInventoryMutation(true);
    private void Start()
    {
        bootstrap = PlayFabBootstrap.Instance;
        if (bootstrap == null)
        {
            Debug.LogError("PlayFabBootstrap을 찾을 수 없습니다.");
            return;
        }

        if (bootstrap.IsReady)
        {
            LoadInventory();
            return;
        }

        bootstrap.Ready += LoadInventory;
    }

    private void OnDestroy()
    {
        if (bootstrap != null)
            bootstrap.Ready -= LoadInventory;
    }

    public void LoadInventory()
    {
        if (bootstrap == null || !bootstrap.IsReady)
            return;
        if (isInventoryMutationInProgress || isInventoryLoadInProgress)
        {
            inventoryReloadPending = true;
            return;
        }

        isInventoryLoadInProgress = true;
        PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), OnInventoryLoaded, OnRequestFailed);
    }

    private void OnInventoryLoaded(GetUserInventoryResult result)
    {
        isInventoryLoadInProgress = false;
        Dictionary<string, long> quantities = new();
        if (result.Inventory != null)
        {
            foreach (ItemInstance item in result.Inventory)
            {
                if (string.IsNullOrWhiteSpace(item.ItemId))
                    continue;
                long amount = item.RemainingUses.GetValueOrDefault(1);
                if (quantities.TryGetValue(item.ItemId, out long currentAmount))
                    quantities[item.ItemId] = currentAmount + amount;
                else
                    quantities.Add(item.ItemId, amount);
            }
        }

        int gold = 0;
        if (result.VirtualCurrency != null)
            result.VirtualCurrency.TryGetValue(GoldCurrencyCode, out gold);
        Gold = gold;
        if (warehouse == null)
        {
            Debug.LogError("PlayFabInventoryService에 WarehouseController가 연결되지 않았습니다.");
            return;
        }

        foreach (var definition in GameManager.Instance.CropDefinitions)
            if (definition != null)
                warehouse.SetStock(definition.CropType, GetQuantity(quantities, definition.ServerId));
        Debug.Log($"PlayFab 창고 동기화 성공\nGold: {Gold}");
        InventoryUpdated?.Invoke();
        if (!checkedSaleRecovery)
        {
            RecoverSale();
            return;
        }

        ProcessPendingInventoryReload();
    }

    public void DepositCrop(CropType cropType, long amount, Action onSuccess, Action<string> onFailure)
    {
        onFailure?.Invoke("개별 입고 대신 창고의 일괄 입고 기능을 사용해야 합니다.");
    }

    public void DepositHarvestBatch(string batchId, IReadOnlyList<CropDepositRequest> entries, Action<CropDepositBatchResult> onSuccess, Action<string> onFailure)
    {
        if (!Guid.TryParseExact(batchId, "N", out _))
        {
            onFailure?.Invoke("입고 배치 ID가 올바르지 않습니다.");
            return;
        }

        if (!CanSubmitInventoryMutation)
        {
            onFailure?.Invoke("다른 창고 작업이 처리 중입니다.");
            return;
        }

        if (entries == null || entries.Count == 0 || entries.Count > 64)
        {
            onFailure?.Invoke("입고 내역은 1개 이상 64개 이하여야 합니다.");
            return;
        }

        List<CropDepositRequest> depositEntries = new();
        HashSet<string> uniqueIds = new();
        long totalAmount = 0;
        foreach (CropDepositRequest entry in entries)
        {
            if (entry == null || !Guid.TryParseExact(entry.harvestId, "N", out _) || !uniqueIds.Add(entry.harvestId) || entry.amount < 1 || totalAmount > 64 - entry.amount)
            {
                onFailure?.Invoke("입고 내역이 올바르지 않거나 한 번에 64개를 초과했습니다.");
                return;
            }

            totalAmount += entry.amount;
            depositEntries.Add(entry);
        }

        ExecuteCloudScriptRequest request = new()
        {
            FunctionName = "depositCropBatch",
            FunctionParameter = new
            {
                batchId,
                entries = depositEntries
            },
            RevisionSelection = CloudScriptRevisionOption.Live
        };
        isInventoryMutationInProgress = true;
        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            if (result.Error != null)
            {
                EndInventoryMutation(true);
                string message = $"{result.Error.Error}: {result.Error.Message}";
                onFailure?.Invoke(message);
                return;
            }

            if (!TryReadDepositResult(result.FunctionResult, out CropDepositBatchResult depositResult))
            {
                EndInventoryMutation(true);
                onFailure?.Invoke("입고 응답 형식이 올바르지 않습니다.");
                return;
            }

            if (!string.Equals(depositResult.BatchId, batchId, StringComparison.Ordinal))
            {
                EndInventoryMutation(true);
                onFailure?.Invoke("입고 응답의 배치 ID가 요청과 일치하지 않습니다.");
                return;
            }

            EndInventoryMutation(false);
            inventoryReloadPending = false;
            Debug.Log($"작물 {depositResult.TotalAmount}개 서버 일괄 입고 성공");
            onSuccess?.Invoke(depositResult);
        }, error =>
        {
            EndInventoryMutation(true);
            onFailure?.Invoke(error.GenerateErrorReport());
        });
    }

    private void OnRequestFailed(PlayFabError error)
    {
        isInventoryLoadInProgress = false;
        Debug.LogError($"PlayFab 인벤토리 조회 실패\n{error.GenerateErrorReport()}");
        ProcessPendingInventoryReload();
    }

    private void EndInventoryMutation(bool processPendingReload)
    {
        isInventoryMutationInProgress = false;
        if (processPendingReload)
            ProcessPendingInventoryReload();
    }

    private void ProcessPendingInventoryReload()
    {
        if (!inventoryReloadPending || isInventoryMutationInProgress || isInventoryLoadInProgress)
            return;
        inventoryReloadPending = false;
        LoadInventory();
    }

    private static bool TryGetItemId(CropType cropType, out string itemId)
    {
        itemId = null;
        if (GameManager.Instance == null || !GameManager.Instance.TryGetCropDefinition(cropType, out var definition))
            return false;
        itemId = definition.ServerId;
        return true;
    }

    private static long GetQuantity(Dictionary<string, long> quantities, string itemId)
    {
        return quantities.TryGetValue(itemId, out long quantity) ? quantity : 0;
    }

    private static bool TryReadDepositResult(object functionResult, out CropDepositBatchResult depositResult)
    {
        depositResult = default;
        if (functionResult is not IDictionary<string, object> data || !data.TryGetValue("batchId", out object batchIdValue) || !data.TryGetValue("totalAmount", out object totalAmountValue) || !data.TryGetValue("confirmedTotals", out object totalsValue))
            return false;
        string batchId = Convert.ToString(batchIdValue);
        if (!Guid.TryParseExact(batchId, "N", out _) || totalsValue is not IDictionary<string, object> totalsData)
            return false;
        Dictionary<CropType, long> confirmedTotals = new();
        if (GameManager.Instance == null)
            return false;
        foreach (var definition in GameManager.Instance.CropDefinitions)
            if (definition != null && !TryAddConfirmedTotal(totalsData, definition.ServerId, definition.CropType, confirmedTotals))
                return false;
        long totalAmount = Convert.ToInt64(totalAmountValue);
        bool wasDuplicate = data.TryGetValue("duplicate", out object duplicateValue) && Convert.ToBoolean(duplicateValue);
        depositResult = new CropDepositBatchResult(batchId, totalAmount, wasDuplicate, confirmedTotals);
        return true;
    }

    private static bool TryAddConfirmedTotal(IDictionary<string, object> totalsData, string itemId, CropType cropType, Dictionary<CropType, long> confirmedTotals)
    {
        long amount = 0;
        if (totalsData.TryGetValue(itemId, out object amountValue))
        {
            try
            {
                amount = Convert.ToInt64(amountValue);
            }
            catch (Exception)
            {
                return false;
            }
        }

        if (amount < 0)
            return false;
        confirmedTotals[cropType] = amount;
        return true;
    }

    private static bool TryReadSaleResult(object functionResult, CropType cropType, out CropSaleResult saleResult)
    {
        saleResult = default;
        if (functionResult is not IDictionary<string, object> data)
            return false;
        if (!data.TryGetValue("amount", out object amountValue) || !data.TryGetValue("unitPrice", out object unitPriceValue) || !data.TryGetValue("totalPrice", out object totalPriceValue))
            return false;
        long amount = Convert.ToInt64(amountValue);
        int unitPrice = Convert.ToInt32(unitPriceValue);
        long totalPrice = Convert.ToInt64(totalPriceValue);
        long cooldownEndUnixSeconds = data.TryGetValue("cooldownEndUnixSeconds", out object cooldownValue) ? Convert.ToInt64(cooldownValue) : 0L;
        saleResult = new CropSaleResult(cropType, amount, unitPrice, totalPrice, cooldownEndUnixSeconds);
        return true;
    }
}
