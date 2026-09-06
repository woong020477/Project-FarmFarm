// 역할: 퀘스트·확장·용병·수리 요청과 서버 진행 상태를 연결한다.
using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Json;
using UnityEngine;

public sealed class Progression : MonoBehaviour
{
    public ProgressState State { get; private set; } = new();
    public bool IsReady { get; private set; }
    public bool IsBusy { get; private set; }
    public string Status { get; private set; } = "서버 연결 대기";

    public event Action Changed;
    private float nextSync;
    private bool wasPaused;
    private string unresolvedRequest;
    private int unresolvedDamage;
    private GameManager Manager => GameManager.Instance;
    public long Now => Manager.Clock != null ? Manager.Clock.UnixTimeSeconds : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private void Update()
    {
        if (wasPaused || Time.unscaledTime < nextSync || IsBusy || Manager == null || Manager.Bootstrap == null || !Manager.Bootstrap.IsReady || Manager.FarmSave == null || !Manager.FarmSave.IsLoaded)
            return;
        if (Manager.Inventory == null || !Manager.Inventory.CanSubmitInventoryMutation)
        {
            nextSync = Time.unscaledTime + 1;
            return;
        }

        nextSync = Time.unscaledTime + 120;
        Request(IsReady ? "heartbeat" : "load");
    }

    private void OnApplicationPause(bool paused)
    {
        wasPaused = paused;
        if (paused && IsReady)
            Request("checkpoint");
        if (!paused)
        {
            IsReady = false;
            nextSync = 0;
        }
    }

    private void OnApplicationQuit()
    {
        if (isActiveAndEnabled && IsReady && !IsBusy)
            Request("checkpoint");
    }

    public void Reload()
    {
        if (!IsBusy)
        {
            IsReady = false;
            nextSync = 0;
        }
    }

    public void UnlockPlot() => Request("plot");
    public void BuyDrone() => Request("drone");
    public void SubmitQuest(string id) => Request("quest", id);
    public void Hire(int tier) => Request("hire", tier.ToString());
    public void SaveDefense() => Request("checkpoint");
    public void RepairBarricade() => Request("repair");
    private void Request(string action, string target = "")
    {
        if (IsBusy || Manager == null || Manager.Inventory == null || (action != "load" && !IsReady))
            return;
        if (action != "load" && Now - State.serverTime < 2)
        {
            const string wait = "처리 간격 대기 중입니다. 잠시 후 다시 시도하세요.";
            if (Status != wait)
            {
                Status = wait;
                Changed?.Invoke();
            }

            return;
        }

        var inventory = Manager.Inventory;
        if (!inventory.TryBeginSharedMutation())
        {
            Status = "창고 작업 처리 중. 잠시 후 다시 시도하세요.";
            Changed?.Invoke();
            return;
        }

        IsBusy = true;
        var defense = Manager.Defense;
        int damage = defense == null ? 0 : defense.PendingDamage;
        var health = defense == null ? Array.Empty<MercenaryState>() : defense.CaptureMercenaries();
        string requestId = Guid.NewGuid().ToString("N");
        if (action != "load")
        {
            unresolvedRequest = requestId;
            unresolvedDamage = damage;
        }

        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest { FunctionName = "farmProgress", RevisionSelection = CloudScriptRevisionOption.Live, FunctionParameter = new { action, target, requestId, revision = State.revision, damage, mercenaries = health } }, result =>
        {
            IsBusy = false;
            inventory.EndSharedMutation();
            if (result.Error != null)
            {
                IsReady = false;
                Status = "진행 처리 실패: " + result.Error.Message;
                nextSync = Time.unscaledTime + 30;
                Changed?.Invoke();
                return;
            }

            try
            {
                var next = JsonUtility.FromJson<ProgressState>(PlayFabSimpleJson.SerializeObject(result.FunctionResult));
                if (next == null || next.version != 1)
                    throw new InvalidOperationException("진행 응답 형식 오류");
                State = next;
                IsReady = true;
                if (action != "load")
                    defense?.AcknowledgeDamage(damage);
                else if (!string.IsNullOrEmpty(unresolvedRequest) && Array.IndexOf(next.receipts, unresolvedRequest) >= 0)
                    defense?.AcknowledgeDamage(unresolvedDamage);
                unresolvedRequest = null;
                unresolvedDamage = 0;
                Manager.RestoreOwnership(next.plots, next.drones);
                Manager.PlayerProfile.Restore(next.level, next.experience, Manager.PlayerProfile.Title);
                defense?.Restore(next);
                Status = action == "repair" ? "바리케이트 수리 완료" : action == "quest" ? "퀘스트 제출 및 보상 완료" : action == "plot" ? "농지 해금 완료" : action == "drone" ? "드론 구매 완료" : action == "hire" ? "용병 지원 요청 완료" : "동기화 완료";
                if (action == "repair" || action == "quest" || action == "plot" || action == "drone" || action == "hire" || damage > 0)
                    inventory.LoadInventory();
                if (action == "repair" || action == "quest" || action == "plot" || action == "drone" || action == "hire")
                    Manager.FarmSave.RequestCriticalSave();
                if (next.recoveryRequired)
                    Status = "이전 거래 확인 필요: 관리자에게 progress_op 확인 요청";
            }
            catch (Exception e)
            {
                IsReady = false;
                nextSync = Time.unscaledTime + 30;
                Status = e.Message;
            }

            Changed?.Invoke();
        }, error =>
        {
            IsBusy = false;
            IsReady = false;
            inventory.EndSharedMutation();
            Status = "서버 응답 확인 실패. 다시 동기화해 상태를 확인하세요.";
            nextSync = Time.unscaledTime + 30;
            Changed?.Invoke();
        });
    }
}
