// 역할: 판매 수량 입력과 서버 판매 요청 결과를 표시한다. 재고/골드의 권한은 소유하지 않는다.
using System;
using System.Collections.Generic;
using Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TradeUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField]
    private GameObject panel;
    [Header("Data")]
    [SerializeField]
    private PlayFabInventoryService inventory;
    [SerializeField]
    private WarehouseController warehouse;
    [SerializeField]
    private Market market;
    [SerializeField]
    private ServerClock clock;
    [Header("Selected Crop")]
    [SerializeField]
    private Image cropImage;
    [SerializeField]
    private TMP_Text sellNameText;
    [SerializeField]
    private TMP_Text priceText;
    [SerializeField]
    private TMP_Text stockText;
    [SerializeField]
    private TMP_Text goldText;
    [Header("Trade")]
    [SerializeField]
    private TMP_InputField amountInput;
    [SerializeField]
    private TMP_Text statusText;
    [SerializeField]
    private Button sellButton;
    [SerializeField]
    private TMP_Text sellButtonText;
    private readonly Dictionary<CropType, long> cooldownEndByCrop = new();
    private CropDefinition selectedCrop;
    private bool isSelling;
    private float nextRefreshTime;
    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.InventoryUpdated += Refresh;
            inventory.SaleUpdated += RefreshSaleStatus;
        }

        if (warehouse != null)
            warehouse.StockChanged += Refresh;
        if (market != null)
            market.StateChanged += OnMarketStateChanged;
        if (amountInput != null)
            amountInput.onValueChanged.AddListener(OnAmountChanged);
    }

    private void Start()
    {
        if (clock == null)
            clock = ServerClock.Instance;
        if (panel != null)
            panel.SetActive(false);
        SetAmount(0);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;
        nextRefreshTime = Time.unscaledTime + 0.25f;
        RefreshCooldown();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.InventoryUpdated -= Refresh;
            inventory.SaleUpdated -= RefreshSaleStatus;
        }

        if (warehouse != null)
            warehouse.StockChanged -= Refresh;
        if (market != null)
            market.StateChanged -= OnMarketStateChanged;
        if (amountInput != null)
            amountInput.onValueChanged.RemoveListener(OnAmountChanged);
    }

    public void Open(CropDefinition definition)
    {
        if (definition == null || panel == null)
            return;
        if (market == null || !market.IsOpen)
            return;
        selectedCrop = definition;
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        SetAmount(0);
        SetStatus(inventory != null && inventory.HasPendingSale ? inventory.SaleStatus : string.Empty);
        Refresh();
    }

    public void Close()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void AddAmount1()
    {
        AddAmount(1);
    }

    public void AddAmount10()
    {
        AddAmount(10);
    }

    public void AddAmount100()
    {
        AddAmount(100);
    }

    public void SetAll()
    {
        SetAmount(GetSelectedStock());
        RefreshButton();
    }

    public void Sell()
    {
        if (isSelling || selectedCrop == null)
            return;
        if (inventory == null || warehouse == null || market == null)
        {
            SetStatus("판매 시스템이 연결되지 않았습니다.");
            return;
        }

        if (inventory.HasPendingSale)
        {
            inventory.ResumeSale();
            SetStatus(inventory.SaleStatus);
            RefreshButton();
            return;
        }

        if (!market.IsOpen)
        {
            SetStatus("헬리콥터가 떠나 판매가 취소되었습니다.");
            Close();
            return;
        }

        if (TryGetCooldown(selectedCrop.CropType, out int remainingSeconds))
        {
            SetStatus($"{remainingSeconds}초 후 다시 판매할 수 있습니다.");
            RefreshButton();
            return;
        }

        if (!TryGetAmount(out long requestedAmount))
        {
            SetStatus("판매 수량을 1개 이상 입력해주세요.");
            return;
        }

        long stock = GetSelectedStock();
        if (stock <= 0)
        {
            SetStatus("판매할 작물이 없습니다.");
            RefreshButton();
            return;
        }

        long saleAmount = Math.Min(requestedAmount, stock);
        CropType cropType = selectedCrop.CropType;
        string displayName = selectedCrop.DisplayName;
        isSelling = true;
        SetStatus($"{displayName} {saleAmount}개 판매 중...");
        RefreshButton();
        inventory.SellCrop(cropType, saleAmount, result =>
        {
            isSelling = false;
            cooldownEndByCrop[result.CropType] = result.CooldownEndUnixSeconds > 0 ? result.CooldownEndUnixSeconds : CurrentUnixSeconds + 10;
            SetAmount(0);
            SetStatus(inventory.SaleStatus);
            Refresh();
        }, error =>
        {
            isSelling = false;
            SetStatus($"판매 실패: {error}");
            RefreshButton();
        });
    }

    private long CurrentUnixSeconds => clock != null && clock.IsReady ? clock.UnixTimeSeconds : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private void AddAmount(long amount)
    {
        long current = TryGetAmount(out long parsedAmount) ? parsedAmount : 0;
        long nextAmount = current > long.MaxValue - amount ? long.MaxValue : current + amount;
        SetAmount(nextAmount);
        RefreshButton();
    }

    private void Refresh()
    {
        if (goldText != null)
            goldText.text = $"{(inventory == null ? 0 : inventory.Gold)}G";
        if (selectedCrop == null)
            return;
        if (cropImage != null)
        {
            cropImage.sprite = selectedCrop.MarketSprite;
            cropImage.enabled = selectedCrop.MarketSprite != null;
        }

        if (sellNameText != null)
            sellNameText.text = selectedCrop.DisplayName;
        if (priceText != null)
            priceText.text = $"{(market == null ? 0 : market.GetPrice(selectedCrop.CropType))}G";
        if (stockText != null)
            stockText.text = $"{GetSelectedStock()}개";
        RefreshCooldown();
    }

    private void RefreshCooldown()
    {
        int remainingSeconds = 0;
        bool coolingDown = selectedCrop != null && TryGetCooldown(selectedCrop.CropType, out remainingSeconds);
        if (sellButtonText != null)
        {
            int minutes = remainingSeconds / 60;
            int seconds = remainingSeconds % 60;
            sellButtonText.text = coolingDown ? $"판매대기\n{minutes:00}:{seconds:00}" : "판매하기";
            if (inventory != null && inventory.HasPendingSale)
                sellButtonText.text = inventory.CanResumeSale ? "판매 확인" : "판매 처리 확인 중";
        }

        RefreshButton();
    }

    private void RefreshSaleStatus()
    {
        SetStatus(inventory.SaleStatus);
        RefreshButton();
    }

    private void RefreshButton()
    {
        if (sellButton == null)
            return;
        if (inventory != null && inventory.HasPendingSale)
        {
            sellButton.interactable = inventory.CanResumeSale;
            return;
        }

        bool hasAmount = TryGetAmount(out long amount) && amount > 0;
        bool coolingDown = selectedCrop != null && TryGetCooldown(selectedCrop.CropType, out _);
        sellButton.interactable = !isSelling && !coolingDown && selectedCrop != null && market != null && market.IsOpen && GetSelectedStock() > 0 && hasAmount;
    }

    public bool TryGetCooldown(CropType cropType, out int remainingSeconds)
    {
        remainingSeconds = 0;
        cooldownEndByCrop.TryGetValue(cropType, out long endUnixSeconds);
        if (inventory != null)
            endUnixSeconds = Math.Max(endUnixSeconds, inventory.GetSaleCooldownEnd(cropType));
        if (endUnixSeconds <= 0)
            return false;
        long remaining = endUnixSeconds - CurrentUnixSeconds;
        if (remaining <= 0)
        {
            cooldownEndByCrop.Remove(cropType);
            return false;
        }

        remainingSeconds = remaining > int.MaxValue ? int.MaxValue : (int)remaining;
        return true;
    }

    private void OnMarketStateChanged()
    {
        if (market != null && !market.IsOpen)
            Close();
        RefreshButton();
    }

    private void OnAmountChanged(string value)
    {
        RefreshButton();
    }

    private long GetSelectedStock()
    {
        return selectedCrop == null || warehouse == null ? 0 : warehouse.GetConfirmedStock(selectedCrop.CropType);
    }

    private void SetAmount(long amount)
    {
        if (amountInput != null)
            amountInput.text = Math.Max(0, amount).ToString();
    }

    private bool TryGetAmount(out long amount)
    {
        amount = 0;
        return amountInput != null && long.TryParse(amountInput.text, out amount) && amount >= 1;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
