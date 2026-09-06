// 역할: 농지 버튼의 소유 잠금·작물 오버레이·선택 상태를 표시한다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FarmSelectionView : MonoBehaviour
{
    [SerializeField]
    private FarmPlotController farmPlot;
    [SerializeField]
    private FarmUI farmUI;
    [SerializeField]
    private RectTransform overlayRect;
    [SerializeField]
    private Image cropImage;
    [SerializeField]
    private Button selectButton;
    [SerializeField]
    private TMP_Text lockText;
    [SerializeField]
    private TMP_Text levelText;
    [Range(0f, 1f)]
    [SerializeField]
    private float idleAlpha = 0.25f;
    private GameManager gameManager;
    private bool isSelected;
    private void OnEnable()
    {
        if (farmPlot != null)
            farmPlot.SelectionChanged += Refresh;
        if (selectButton != null)
            selectButton.onClick.AddListener(OpenFarmPanel);
        BindGameManager();
        FitToFarmBounds();
        Refresh();
    }

    private void Start()
    {
        BindGameManager();
        FitToFarmBounds();
        RefreshOwnership();
    }

    private void OnDisable()
    {
        if (farmPlot != null)
            farmPlot.SelectionChanged -= Refresh;
        if (selectButton != null)
            selectButton.onClick.RemoveListener(OpenFarmPanel);
        if (gameManager != null)
            gameManager.FarmPlotOwnershipChanged -= RefreshOwnership;
        gameManager = null;
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshAlpha();
    }

    private void OpenFarmPanel()
    {
        if (gameManager == null || !gameManager.IsFarmPlotUnlocked(farmPlot))
            return;
        FarmUI targetUI = farmUI != null ? farmUI : FarmUI.Instance;
        targetUI?.Open(farmPlot, this);
    }

    private void Refresh()
    {
        if (farmPlot == null || cropImage == null)
            return;
        if (farmPlot.TryGetCropDefinition(farmPlot.DisplayCropType, out CropDefinition definition))
        {
            cropImage.sprite = definition.MarketSprite;
            cropImage.enabled = definition.MarketSprite != null;
        }
        else
        {
            cropImage.sprite = null;
            cropImage.enabled = false;
        }

        RefreshAlpha();
        RefreshOwnership();
    }

    private void BindGameManager()
    {
        GameManager target = GameManager.Instance;
        if (gameManager == target)
            return;
        if (gameManager != null)
            gameManager.FarmPlotOwnershipChanged -= RefreshOwnership;
        gameManager = target;
        if (gameManager != null)
            gameManager.FarmPlotOwnershipChanged += RefreshOwnership;
    }

    private void RefreshOwnership()
    {
        bool isUnlocked = gameManager != null && gameManager.IsFarmPlotUnlocked(farmPlot);
        if (lockText != null)
            lockText.gameObject.SetActive(!isUnlocked);
        if (levelText != null)
        {
            levelText.gameObject.SetActive(!isUnlocked);
            if (gameManager != null)
                for (int i = 0; i < gameManager.FarmPlots.Count; i++)
                    if (gameManager.FarmPlots[i] == farmPlot)
                    {
                        levelText.text = $"Lv. {i + 1}";
                        break;
                    }
        }

        if (selectButton != null)
            selectButton.interactable = isUnlocked;
        if (cropImage != null)
            cropImage.enabled = isUnlocked && cropImage.sprite != null;
        if (!isUnlocked)
            isSelected = false;
        RefreshAlpha();
    }

    private void RefreshAlpha()
    {
        if (cropImage == null)
            return;
        Color color = cropImage.color;
        color.a = isSelected ? 1f : idleAlpha;
        cropImage.color = color;
    }

    private void FitToFarmBounds()
    {
        if (farmPlot == null || overlayRect == null || overlayRect.parent == null)
            return;
        Bounds bounds = farmPlot.GetFarmWorldBounds();
        Vector3 position = bounds.center;
        position.z = overlayRect.position.z;
        overlayRect.position = position;
        Vector3 parentScale = overlayRect.parent.lossyScale;
        float width = Mathf.Approximately(parentScale.x, 0f) ? bounds.size.x : bounds.size.x / Mathf.Abs(parentScale.x);
        float height = Mathf.Approximately(parentScale.y, 0f) ? bounds.size.y : bounds.size.y / Mathf.Abs(parentScale.y);
        overlayRect.sizeDelta = new Vector2(width, height);
    }
}
