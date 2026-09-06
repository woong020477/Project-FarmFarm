// 역할: 작물 목록과 헬기 시간을 표시하고 선택 작물의 거래 패널을 연다.
using System;
using Enum;
using TMPro;
using UnityEngine;

public sealed class MarketUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField]
    private GameObject panel;
    [SerializeField]
    private TradeUI tradeUI;
    [Header("Data")]
    [SerializeField]
    private PlayFabInventoryService inventory;
    [SerializeField]
    private WarehouseController warehouse;
    [SerializeField]
    private Market market;
    [Header("Text")]
    [SerializeField]
    private TMP_Text goldText;
    [SerializeField]
    private TMP_Text marketOpenButtonText;
    [SerializeField]
    private TMP_Text marketOpenButtonTimerText;
    [SerializeField]
    private TMP_Text marketDepartureTimeText;
    [SerializeField]
    private TMP_Text tradeDepartureTimeText;
    private float nextTimeRefresh;
    private bool openWhenReady;
    public event Action Refreshed;
    private void OnEnable()
    {
        if (inventory != null)
            inventory.InventoryUpdated += Refresh;
        if (warehouse != null)
            warehouse.StockChanged += Refresh;
        if (market != null)
            market.StateChanged += OnMarketStateChanged;
    }

    private void Start()
    {
        if (panel != null)
            panel.SetActive(false);
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextTimeRefresh)
            return;
        nextTimeRefresh = Time.unscaledTime + 0.25f;
        RefreshMarketTime();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.InventoryUpdated -= Refresh;
        if (warehouse != null)
            warehouse.StockChanged -= Refresh;
        if (market != null)
            market.StateChanged -= OnMarketStateChanged;
    }

    public void Open()
    {
        if (panel == null)
        {
            Debug.LogError("MarketUI에 Market Panel이 연결되지 않았습니다.");
            return;
        }

        if (market == null || !market.IsReady)
        {
            openWhenReady = true;
            Debug.Log("판매 정보를 불러오는 중입니다.");
            market?.RefreshState();
            return;
        }

        if (!market.IsOpen)
        {
            Debug.Log("현재 헬리콥터가 없어 시장을 열 수 없습니다.");
            return;
        }

        panel.SetActive(true);
        openWhenReady = false;
        UIManager.Instance?.CloseGameplayPanels();
        panel.transform.SetAsLastSibling();
        Refresh();
        inventory?.LoadInventory();
    }

    public void Close()
    {
        openWhenReady = false;
        tradeUI?.Close();
        if (panel != null)
            panel.SetActive(false);
    }

    public void OpenTrade(CropDefinition definition)
    {
        if (definition == null || tradeUI == null)
            return;
        if (market == null || !market.IsOpen)
        {
            Close();
            Debug.Log("헬리콥터가 떠나 거래를 시작할 수 없습니다.");
            return;
        }

        if (!HasPendingSale(definition.CropType) && TryGetTradeCooldown(definition.CropType, out int remainingSeconds))
        {
            Debug.Log($"{definition.DisplayName}은 {remainingSeconds}초 후 다시 판매할 수 있습니다.");
            return;
        }

        tradeUI.Open(definition);
    }

    public int GetPrice(CropType cropType)
    {
        return market == null ? 0 : market.GetPrice(cropType);
    }

    public long GetStock(CropType cropType)
    {
        return warehouse == null ? 0 : warehouse.GetConfirmedStock(cropType);
    }

    public bool CanTrade(CropType cropType)
    {
        if (inventory != null && inventory.HasPendingSale)
            return market != null && market.IsReady && market.IsOpen && HasPendingSale(cropType);
        return market != null && market.IsReady && market.IsOpen && GetPrice(cropType) > 0 && GetStock(cropType) > 0 && !TryGetTradeCooldown(cropType, out _);
    }

    public bool HasPendingSale(CropType cropType) => inventory != null && inventory.HasPendingSaleFor(cropType);
    public bool TryGetTradeCooldown(CropType cropType, out int remainingSeconds)
    {
        if (tradeUI != null)
            return tradeUI.TryGetCooldown(cropType, out remainingSeconds);
        remainingSeconds = 0;
        return false;
    }

    private void OnMarketStateChanged()
    {
        if (openWhenReady && market != null && market.IsReady)
        {
            openWhenReady = false;
            if (market.IsOpen)
                Open();
        }

        if (market != null && !market.IsOpen && panel != null && panel.activeSelf)
        {
            Close();
            Debug.Log("헬리콥터 영업이 종료되어 시장과 거래 창을 닫았습니다.");
        }

        Refresh();
    }

    private void Refresh()
    {
        if (goldText != null)
            goldText.text = $"{(inventory == null ? 0 : inventory.Gold)}G";
        RefreshMarketTime();
        Refreshed?.Invoke();
    }

    private void RefreshMarketTime()
    {
        bool isOpen = market != null && market.IsOpen;
        int seconds = market == null ? 0 : market.RemainingSeconds;
        string formattedTime = $"{seconds / 60:00}:{seconds % 60:00}";
        if (marketOpenButtonText != null)
            marketOpenButtonText.text = isOpen ? "농작물 판매" : "수송헬기";
        if (marketOpenButtonTimerText != null)
        {
            marketOpenButtonTimerText.text = formattedTime;
            marketOpenButtonTimerText.color = isOpen ? Color.green : Color.red;
        }

        if (marketDepartureTimeText != null)
            marketDepartureTimeText.text = isOpen ? formattedTime : "00:00";
        if (tradeDepartureTimeText != null)
            tradeDepartureTimeText.text = isOpen ? formattedTime : "00:00";
    }
}
