// 역할: 작물 이미지·가격·재고와 판매 쿨다운을 시장 목록의 한 행에 표시한다.
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class MarketItem : MonoBehaviour
{
    [Header("Data")]
    [SerializeField]
    private CropDefinition definition;
    [SerializeField]
    private MarketUI marketUI;
    [Header("UI")]
    [SerializeField]
    private Image cropImage;
    [SerializeField]
    private TMP_Text nameText;
    [SerializeField]
    private TMP_Text priceText;
    [SerializeField]
    private TMP_Text stockText;
    [FormerlySerializedAs("sellButton")]
    [SerializeField]
    private Button tradeButton;
    [SerializeField]
    private TMP_Text tradeButtonText;
    private float nextCooldownRefreshTime;
    private void OnEnable()
    {
        if (marketUI != null)
            marketUI.Refreshed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (marketUI != null)
            marketUI.Refreshed -= Refresh;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCooldownRefreshTime)
            return;
        nextCooldownRefreshTime = Time.unscaledTime + 0.25f;
        RefreshTradeButton();
    }

    public void OpenTrade()
    {
        marketUI?.OpenTrade(definition);
    }

    private void Refresh()
    {
        if (definition == null)
            return;
        if (cropImage != null)
        {
            cropImage.sprite = definition.MarketSprite;
            cropImage.enabled = definition.MarketSprite != null;
        }

        if (nameText != null)
            nameText.text = definition.DisplayName;
        if (priceText != null)
            priceText.text = $"{(marketUI == null ? 0 : marketUI.GetPrice(definition.CropType))}G";
        if (stockText != null)
            stockText.text = $"{(marketUI == null ? 0 : marketUI.GetStock(definition.CropType))}개";
        RefreshTradeButton();
    }

    private void RefreshTradeButton()
    {
        if (definition == null)
            return;
        int remainingSeconds = 0;
        bool coolingDown = marketUI != null && marketUI.TryGetTradeCooldown(definition.CropType, out remainingSeconds);
        if (tradeButtonText != null)
            tradeButtonText.text = marketUI != null && marketUI.HasPendingSale(definition.CropType) ? "판매 확인" : coolingDown ? $"쿨타임 {remainingSeconds}초" : "판매하기";
        if (tradeButton != null)
            tradeButton.interactable = marketUI != null && marketUI.CanTrade(definition.CropType);
    }
}
