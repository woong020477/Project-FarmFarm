// 역할: 선택한 농지의 작물 변경 UI와 카메라 포커스 해제를 연결한다.
using System.Collections.Generic;
using Enum;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FarmUI : MonoBehaviour
{
    public static FarmUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField]
    private GameObject panel;
    [SerializeField]
    private TMP_Text statusText;
    [SerializeField]
    private List<FarmCropItem> cropItems = new();
    [Header("Data")]
    [SerializeField]
    private WarehouseController warehouse;
    [SerializeField]
    private PlayFabInventoryService inventory;
    [SerializeField]
    private Market market;
    [SerializeField]
    private CameraManager cameraManager;
    private FarmPlotController selectedFarmPlot;
    private FarmSelectionView selectedView;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (inventory != null)
            inventory.InventoryUpdated += Refresh;
        if (warehouse != null)
            warehouse.StockChanged += Refresh;
        if (market != null)
            market.StateChanged += Refresh;
    }

    private void Start()
    {
        foreach (FarmCropItem cropItem in cropItems)
            cropItem?.Initialize(this);
        if (panel != null)
            panel.SetActive(false);
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.InventoryUpdated -= Refresh;
        if (warehouse != null)
            warehouse.StockChanged -= Refresh;
        if (market != null)
            market.StateChanged -= Refresh;
    }

    private void OnDestroy()
    {
        if (selectedFarmPlot != null)
            selectedFarmPlot.SelectionChanged -= Refresh;
        if (Instance == this)
            Instance = null;
    }

    public void Open(FarmPlotController farmPlot, FarmSelectionView selectionView)
    {
        if (farmPlot == null || panel == null || GameManager.Instance == null || !GameManager.Instance.IsFarmPlotUnlocked(farmPlot))
            return;
        if (selectedFarmPlot != null)
            selectedFarmPlot.SelectionChanged -= Refresh;
        selectedView?.SetSelected(false);
        selectedFarmPlot = farmPlot;
        selectedView = selectionView;
        selectedView?.SetSelected(true);
        selectedFarmPlot.SelectionChanged += Refresh;
        panel.SetActive(true);
        SetStatus(string.Empty);
        Refresh();
        CameraManager targetCameraManager = cameraManager != null ? cameraManager : CameraManager.Instance;
        targetCameraManager?.FocusFarm(farmPlot);
    }

    public void Close()
    {
        if (selectedFarmPlot != null)
            selectedFarmPlot.SelectionChanged -= Refresh;
        selectedView?.SetSelected(false);
        selectedFarmPlot = null;
        selectedView = null;
        if (panel != null)
            panel.SetActive(false);
        CameraManager targetCameraManager = cameraManager != null ? cameraManager : CameraManager.Instance;
        targetCameraManager?.CloseFarmFocus();
    }

    public void SelectCrop(CropDefinition definition)
    {
        if (selectedFarmPlot == null || definition == null || !selectedFarmPlot.TryGetCropDefinition(definition.CropType, out _))
            return;
        selectedFarmPlot.RequestCropChange(definition.CropType);
        if (selectedFarmPlot.ReservedCropType == definition.CropType)
            SetStatus($"수확 가능한 작물을 모두 수확한 뒤 {definition.DisplayName} 심기로 변경됩니다.");
        else
            SetStatus($"{definition.DisplayName} 심기로 변경했습니다.");
        FarmSaveService.Instance?.RequestCriticalSave();
        RefreshItems();
    }

    public long GetStock(CropType cropType)
    {
        return warehouse == null ? 0 : warehouse.GetStock(cropType);
    }

    public int GetPrice(CropType cropType)
    {
        return market == null ? 0 : market.GetPrice(cropType);
    }

    public bool CanSelect(CropType cropType)
    {
        return selectedFarmPlot != null && selectedFarmPlot.TryGetCropDefinition(cropType, out _);
    }

    private void Refresh()
    {
        RefreshItems();
    }

    private void RefreshItems()
    {
        foreach (FarmCropItem cropItem in cropItems)
            cropItem?.Refresh();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
