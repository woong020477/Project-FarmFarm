// 역할: 작물 정의와 보유량을 농지 선택 목록의 한 행에 표시한다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FarmCropItem : MonoBehaviour
{
    [SerializeField]
    private CropDefinition definition;
    [SerializeField]
    private Image cropImage;
    [SerializeField]
    private TMP_Text nameText;
    [SerializeField]
    private TMP_Text quantityText;
    [SerializeField]
    private TMP_Text priceText;
    [SerializeField]
    private TMP_Text growthTimeText;
    [SerializeField]
    private TMP_Text harvestAmountText;
    [SerializeField]
    private Button plantButton;
    private FarmUI farmUI;
    public void Initialize(FarmUI owner)
    {
        farmUI = owner;
        if (plantButton != null)
        {
            plantButton.onClick.RemoveListener(SelectCrop);
            plantButton.onClick.AddListener(SelectCrop);
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (plantButton != null)
            plantButton.onClick.RemoveListener(SelectCrop);
    }

    public void Refresh()
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
        if (quantityText != null)
            quantityText.text = $"보유량: {(farmUI == null ? 0 : farmUI.GetStock(definition.CropType))}개";
        if (priceText != null)
            priceText.text = $"판매가: {(farmUI == null ? definition.BasePrice : farmUI.GetPrice(definition.CropType))}G";
        if (growthTimeText != null)
            growthTimeText.text = $"성장: {FormatSeconds(definition.MinGrowthSeconds)} ~ {FormatSeconds(definition.MaxGrowthSeconds)}";
        if (harvestAmountText != null)
            harvestAmountText.text = $"수확량: {definition.HarvestAmount}개";
        if (plantButton != null)
            plantButton.interactable = farmUI != null && farmUI.CanSelect(definition.CropType);
    }

    private void SelectCrop()
    {
        farmUI?.SelectCrop(definition);
    }

    private static string FormatSeconds(float totalSeconds)
    {
        int roundedSeconds = Mathf.Max(0, Mathf.RoundToInt(totalSeconds));
        return $"{roundedSeconds}초";
    }
}
