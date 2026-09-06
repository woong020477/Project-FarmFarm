// 역할: 판매 요청 ID와 진행 장부를 유지하고 불확실한 거래를 재조회·복구한다.
using System;
using System.Collections;
using System.Collections.Generic;
using Enum;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public sealed partial class PlayFabInventoryService
{
    [Serializable]
    private sealed class PendingSale
    {
        public string requestId;
        public int cropType;
        public long amount;
        public int step;
    }

    private PendingSale pendingSale;
    public string SaleStatus { get; private set; }

    public event Action SaleUpdated;
    public event Action SaleCompleted;
    private bool checkedSaleRecovery;
    private int saleRetryCount;
    private bool saleNeedsReview;
    private Coroutine saleRetryRoutine;
    private readonly Dictionary<CropType, long> completedSaleCooldowns = new();
    private string SaleKey => "FarmFarm.Sale." + bootstrap.PlayFabId;
    public bool HasPendingSale => pendingSale != null;
    public bool CanResumeSale => pendingSale != null && !saleNeedsReview && CanContactInventory;

    public bool HasPendingSaleFor(CropType cropType) => pendingSale != null && pendingSale.cropType == (int)cropType;
    public long GetSaleCooldownEnd(CropType cropType) => completedSaleCooldowns.TryGetValue(cropType, out long end) ? end : 0;
    public void SellCrop(CropType cropType, long amount, Action<CropSaleResult> onSuccess, Action<string> onFailure)
    {
        if (pendingSale != null)
        {
            ResumeSale();
            onFailure?.Invoke("이전 판매를 확인하고 있습니다.");
            return;
        }

        if (!CanSubmitInventoryMutation)
        {
            onFailure?.Invoke("다른 창고 작업 처리 중입니다.");
            return;
        }

        if (amount < 1 || !TryGetItemId(cropType, out _))
        {
            onFailure?.Invoke("판매 수량 또는 작물 설정을 확인해주세요.");
            return;
        }

        pendingSale = new PendingSale
        {
            requestId = Guid.NewGuid().ToString("N"),
            cropType = (int)cropType,
            amount = amount
        };
        saleRetryCount = 0;
        saleNeedsReview = false;
        SavePendingSale();
        isInventoryMutationInProgress = true;
        SendSale(onSuccess, onFailure);
    }

    public void ResumeSale()
    {
        if (!CanResumeSale)
            return;
        isInventoryMutationInProgress = true;
        SendSale(null, null);
    }

    private void SendSale(Action<CropSaleResult> onSuccess, Action<string> onFailure)
    {
        if (pendingSale == null)
        {
            EndInventoryMutation(true);
            return;
        }

        var active = pendingSale;
        TryGetItemId((CropType)active.cropType, out string itemId);
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest { FunctionName = "sellCrop", RevisionSelection = CloudScriptRevisionOption.Live, FunctionParameter = new { active.requestId, itemId, active.amount, active.step } }, result =>
        {
            if (result.Error != null)
            {
                SaleFailed(result.Error.Error + ": " + result.Error.Message, onFailure);
                return;
            }

            try
            {
                if (result.FunctionResult is not IDictionary<string, object> data || Convert.ToString(data["requestId"]) != active.requestId)
                    throw new FormatException("판매 응답 ID 불일치");
                long paid = Convert.ToInt64(data["amount"]);
                if (data.TryGetValue("gold", out var balance) && balance != null)
                {
                    Gold = Convert.ToInt32(balance);
                    InventoryUpdated?.Invoke();
                }

                SaleStatus = $"판매 처리 {paid}/{Convert.ToInt64(data["requestedAmount"])}개";
                SaleUpdated?.Invoke();
                if (Convert.ToBoolean(data["recoveryRequired"]))
                {
                    saleNeedsReview = true;
                    SaleFailed(Convert.ToString(data["message"]), onFailure);
                    return;
                }

                saleRetryCount = 0;
                if (!Convert.ToBoolean(data["complete"]))
                {
                    active.step = Convert.ToInt32(data["step"]);
                    SavePendingSale();
                    StartCoroutine(ContinueSale(onSuccess, onFailure));
                    return;
                }

                if (!TryReadSaleResult(result.FunctionResult, (CropType)active.cropType, out var sale))
                    throw new FormatException("판매 완료 응답 오류");
                completedSaleCooldowns[sale.CropType] = sale.CooldownEndUnixSeconds;
                pendingSale = null;
                PlayerPrefs.DeleteKey(SaleKey);
                PlayerPrefs.Save();
                EndInventoryMutation(false);
                SaleStatus = $"{paid}개 판매 완료 · {sale.TotalPrice}G";
                if (data.TryGetValue("message", out var message) && !string.IsNullOrEmpty(Convert.ToString(message)))
                    SaleStatus += "\n" + message;
                SaleUpdated?.Invoke();
                // A completed, paid server receipt is the only reward-sound trigger.
                if (paid > 0)
                    SaleCompleted?.Invoke();
                onSuccess?.Invoke(sale);
                FarmSaveService.Instance?.RequestCriticalSave();
                LoadInventory();
            }
            catch (Exception e)
            {
                SaleFailed(e.Message, onFailure);
            }
        }, error => SaleFailed(error.ErrorMessage, onFailure));
    }

    private IEnumerator ContinueSale(Action<CropSaleResult> success, Action<string> failure)
    {
        yield return new WaitForSecondsRealtime(.5f);
        SendSale(success, failure);
    }

    private void SaleFailed(string message, Action<string> failure)
    {
        EndInventoryMutation(false);
        SaleStatus = "판매 확인 필요: " + message;
        SaleUpdated?.Invoke();
        failure?.Invoke(SaleStatus);
        LoadInventory();
        if (!saleNeedsReview && pendingSale != null && saleRetryCount < 2 && saleRetryRoutine == null)
            saleRetryRoutine = StartCoroutine(RetrySale());
    }

    private IEnumerator RetrySale()
    {
        var requestId = pendingSale.requestId;
        saleRetryCount++;
        yield return new WaitForSecondsRealtime(saleRetryCount == 1 ? 2f : 5f);
        float deadline = Time.realtimeSinceStartup + 15f;
        while (pendingSale != null && pendingSale.requestId == requestId && !CanContactInventory && Time.realtimeSinceStartup < deadline)
            yield return null;
        saleRetryRoutine = null;
        // Check whether the server actually accepted the request before resending it.
        // Business-rule rejection may have happened before any receipt was created.
        if (pendingSale != null && pendingSale.requestId == requestId && CanResumeSale)
            RecoverSale();
    }

    private void SavePendingSale()
    {
        PlayerPrefs.SetString(SaleKey, JsonUtility.ToJson(pendingSale));
        PlayerPrefs.Save();
    }

    private void RecoverSale()
    {
        checkedSaleRecovery = true;
        // Keep the local request barrier when the recovery query itself is offline.
        // Only a successful server response may clear it.
        try
        {
            if (PlayerPrefs.HasKey(SaleKey))
                pendingSale = JsonUtility.FromJson<PendingSale>(PlayerPrefs.GetString(SaleKey));
        }
        catch (Exception)
        {
            SaleStatus = "로컬 판매 기록을 읽지 못해 서버 기록을 확인합니다.";
        }

        isInventoryMutationInProgress = true;
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest { FunctionName = "getSaleState", RevisionSelection = CloudScriptRevisionOption.Live }, result =>
        {
            EndInventoryMutation(false);
            if (result.Error != null)
            {
                SaleStatus = "판매 복구 조회 실패: 서버 스크립트 버전을 확인해주세요.";
                SaleUpdated?.Invoke();
                return;
            }

            try
            {
                if (result.FunctionResult is IDictionary<string, object> d && d.TryGetValue("requestId", out var id) && !string.IsNullOrEmpty(Convert.ToString(id)))
                {
                    string serverItem = Convert.ToString(d["itemId"]);
                    CropDefinition definition = null;
                    foreach (var crop in GameManager.Instance.CropDefinitions)
                        if (crop != null && crop.ServerId == serverItem)
                            definition = crop;
                    if (definition == null)
                    {
                        SaleStatus = "복구 대상 작물 설정 누락";
                        SaleUpdated?.Invoke();
                        return;
                    }

                    pendingSale = new PendingSale
                    {
                        requestId = Convert.ToString(id),
                        cropType = (int)definition.CropType,
                        amount = Convert.ToInt64(d["requestedAmount"]),
                        step = Convert.ToInt32(d["step"])
                    };
                    SavePendingSale();
                    if (Convert.ToBoolean(d["recoveryRequired"]))
                    {
                        saleNeedsReview = true;
                        SaleStatus = Convert.ToString(d["message"]);
                        SaleUpdated?.Invoke();
                        return;
                    }

                    ResumeSale();
                }
                else
                {
                    bool hadPending = pendingSale != null;
                    pendingSale = null;
                    PlayerPrefs.DeleteKey(SaleKey);
                    PlayerPrefs.Save();
                    if (hadPending)
                    {
                        SaleStatus = "진행 중인 판매가 없습니다. 서버 재고와 골드를 갱신합니다.";
                        SaleUpdated?.Invoke();
                        LoadInventory();
                    }
                }
            }
            catch (Exception e)
            {
                SaleStatus = "판매 복구 응답 확인 필요: " + e.Message;
                SaleUpdated?.Invoke();
            }
        }, error =>
        {
            EndInventoryMutation(false);
            SaleStatus = "판매 복구 조회 실패: " + error.ErrorMessage;
            SaleUpdated?.Invoke();
        });
    }
}
