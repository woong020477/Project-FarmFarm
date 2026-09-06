// 역할: 장면의 공유 참조와 농지·드론 소유 상태를 등록하고 작업 대상을 선택한다.
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class GameManager : MonoBehaviour
{
    public const int MaxFarmPlotCount = 8;
    public const int MaxDroneCount = 64;
    public static GameManager Instance { get; private set; }

    [Header("Farm Plots")]
    [SerializeField]
    private List<FarmPlotController> farmPlots = new();
    [Range(1, MaxFarmPlotCount)]
    [SerializeField]
    private int unlockedFarmPlotCount = 1;
    [Header("Farm Agents")]
    [SerializeField]
    private PlayerController player;
    [SerializeField]
    private PlayerProfile playerProfile;
    [SerializeField]
    private List<DroneController> drones = new();
    [SerializeField]
    private DroneController dronePrefab;
    [SerializeField]
    private Transform droneSpawnPoint;
    [SerializeField]
    private Transform droneParent;
    [Min(1)]
    [SerializeField]
    private int ownedDroneCount = 1;
    [Header("Warehouses")]
    [SerializeField]
    private List<WarehouseController> warehouses = new();
    [Header("Crop Catalog")]
    [SerializeField]
    private List<CropDefinition> cropDefinitions = new();
    [Header("Shared World References (UI stays with UIManager)")]
    [SerializeField]
    private CameraController cameraController;
    [SerializeField]
    private CameraManager cameraManager;
    [SerializeField]
    private GameObject helicopter;
    [SerializeField]
    private Market market;
    [SerializeField]
    private ServerClock serverClock;
    [SerializeField]
    private GameEvents gameEvents;
    [SerializeField]
    private PlayFabInventoryService inventory;
    [SerializeField]
    private PlayFabBootstrap bootstrap;
    [SerializeField]
    private FarmSaveService farmSave;
    [SerializeField]
    private FarmAuthorityService farmAuthority;
    [SerializeField]
    private List<UnityEngine.Tilemaps.Tilemap> worldTilemaps = new();
    [SerializeField]
    private List<FarmFarm.Exteriors.BuildingFade> buildingFades = new();
    private readonly Dictionary<Enum.CropType, CropDefinition> cropsByType = new();
    [Header("Progression and Defense")]
    [SerializeField]
    private Progression progression;
    [SerializeField]
    private Defense defense;
    public Progression Progression => progression;
    public Defense Defense => defense;
    public IReadOnlyList<CropDefinition> CropDefinitions => cropDefinitions;
    public IReadOnlyList<UnityEngine.Tilemaps.Tilemap> WorldTilemaps => worldTilemaps;
    public IReadOnlyList<FarmFarm.Exteriors.BuildingFade> BuildingFades => buildingFades;
    public WarehouseController Warehouse => warehouses.Count > 0 ? warehouses[0] : null;
    public CameraController CameraController => cameraController;
    public CameraManager Cameras => cameraManager;
    public GameObject Helicopter => helicopter;
    public Market Market => market;
    public ServerClock Clock => serverClock;
    public GameEvents Events => gameEvents;
    public PlayFabInventoryService Inventory => inventory;
    public PlayFabBootstrap Bootstrap => bootstrap;
    public FarmSaveService FarmSave => farmSave;
    public FarmAuthorityService FarmAuthority => farmAuthority;

    private readonly List<WarehouseController> cachedWarehouses = new();
    private readonly List<FarmPlotController> cachedFarmPlots = new();
    private readonly List<DroneController> cachedDrones = new();
    public IReadOnlyList<FarmPlotController> FarmPlots => cachedFarmPlots;
    public IReadOnlyList<DroneController> Drones => cachedDrones;
    public PlayerController Player => player;
    public PlayerProfile PlayerProfile => playerProfile;
    public DroneController PrimaryDrone => cachedDrones.Count == 0 ? null : cachedDrones[0];
    public int UnlockedFarmPlotCount => unlockedFarmPlotCount;
    public int OwnedDroneCount => ownedDroneCount;

    public event System.Action FarmPlotOwnershipChanged;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CacheCropDefinitions();
        CacheFarmPlots();
        CacheWarehouses();
        CacheDrones();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool InteractAt(Vector3 worldPosition, CropCarrier cropCarrier)
    {
        foreach (FarmPlotController farmPlot in cachedFarmPlots)
        {
            if (!IsFarmPlotUnlocked(farmPlot))
                continue;
            if (!farmPlot.ContainsWorldPosition(worldPosition))
                continue;
            farmPlot.InteractAt(worldPosition, cropCarrier);
            return true;
        }

        return false;
    }

    public bool TryWaterAt(Vector3 worldPosition)
    {
        foreach (FarmPlotController farmPlot in cachedFarmPlots)
        {
            if (!IsFarmPlotUnlocked(farmPlot))
                continue;
            if (!farmPlot.ContainsWorldPosition(worldPosition))
                continue;
            Vector3Int cellPosition = farmPlot.WorldToCell(worldPosition);
            return farmPlot.TryPerformWork(FarmWorkType.Water, cellPosition, null);
        }

        return false;
    }

    public bool TryFindClosestTask(Vector3 worldPosition, FarmWorkType workType, out FarmTaskTarget target)
    {
        return TryFindClosestTask(worldPosition, workType, false, out target);
    }

    public bool TryFindClosestTask(Vector3 worldPosition, FarmWorkType workType, bool allowDiagonalMovement, out FarmTaskTarget target)
    {
        target = default;
        FarmPlotController closestFarmPlot = null;
        Vector3Int closestCellPosition = default;
        float closestDistance = float.MaxValue;
        foreach (FarmPlotController farmPlot in cachedFarmPlots)
        {
            if (!IsFarmPlotUnlocked(farmPlot))
                continue;
            if (!farmPlot.TryGetClosestWorkCell(worldPosition, workType, allowDiagonalMovement, out Vector3Int cellPosition, out float distance))
                continue;
            if (distance >= closestDistance)
                continue;
            closestDistance = distance;
            closestFarmPlot = farmPlot;
            closestCellPosition = cellPosition;
        }

        if (closestFarmPlot == null)
            return false;
        target = new FarmTaskTarget(closestFarmPlot, closestCellPosition, workType);
        return true;
    }

    public bool IsTaskAvailable(FarmTaskTarget target, CropCarrier cropCarrier)
    {
        if (target.FarmPlot == null || !IsFarmPlotUnlocked(target.FarmPlot))
            return false;
        if (target.WorkType == FarmWorkType.Harvest && (cropCarrier == null || !cropCarrier.CanCarry))
            return false;
        return target.FarmPlot.IsWorkAvailable(target.WorkType, target.CellPosition);
    }

    public bool TryPerformTask(FarmTaskTarget target, CropCarrier cropCarrier)
    {
        if (target.FarmPlot == null || !IsFarmPlotUnlocked(target.FarmPlot))
            return false;
        return target.FarmPlot.TryPerformWork(target.WorkType, target.CellPosition, cropCarrier);
    }

    private void CacheFarmPlots()
    {
        cachedFarmPlots.Clear();
        if (farmPlots.Count > MaxFarmPlotCount)
            Debug.LogWarning($"농지는 최대 {MaxFarmPlotCount}개까지만 등록됩니다. 초과 항목은 사용하지 않습니다.");
        for (int index = 0; index < farmPlots.Count && cachedFarmPlots.Count < MaxFarmPlotCount; index++)
        {
            FarmPlotController farmPlot = farmPlots[index];
            if (farmPlot == null || cachedFarmPlots.Contains(farmPlot))
                continue;
            cachedFarmPlots.Add(farmPlot);
        }

        unlockedFarmPlotCount = Mathf.Clamp(unlockedFarmPlotCount, 1, Mathf.Max(1, cachedFarmPlots.Count));
        for (int index = 0; index < cachedFarmPlots.Count; index++)
            cachedFarmPlots[index].gameObject.SetActive(index < unlockedFarmPlotCount);
        FarmPlotOwnershipChanged?.Invoke();
        Debug.Log($"농지 {cachedFarmPlots.Count}개 등록, {unlockedFarmPlotCount}개 해금 상태입니다.");
    }

    public bool TryFindClosestWarehouse(Vector3 worldPosition, out WarehouseController warehouse)
    {
        warehouse = null;
        float closestDistance = float.MaxValue;
        foreach (WarehouseController candidate in cachedWarehouses)
        {
            if (candidate == null)
                continue;
            Vector3 position = candidate.DepositPosition;
            float distance = Mathf.Abs(position.x - worldPosition.x) + Mathf.Abs(position.y - worldPosition.y);
            if (distance >= closestDistance)
                continue;
            closestDistance = distance;
            warehouse = candidate;
        }

        return warehouse != null;
    }

    public bool IsHarvestPendingDeposit(string harvestId)
    {
        foreach (WarehouseController warehouse in cachedWarehouses)
        {
            if (warehouse != null && warehouse.ContainsPendingHarvest(harvestId))
                return true;
        }

        return false;
    }

    private void CacheWarehouses()
    {
        cachedWarehouses.Clear();
        foreach (WarehouseController warehouse in warehouses)
        {
            if (warehouse == null || cachedWarehouses.Contains(warehouse))
                continue;
            cachedWarehouses.Add(warehouse);
        }

        Debug.Log($"창고 {cachedWarehouses.Count}개가 GameManager에 등록되었습니다.");
    }

    private void CacheDrones()
    {
        cachedDrones.Clear();
        foreach (DroneController drone in drones)
        {
            if (drone == null || cachedDrones.Contains(drone))
                continue;
            cachedDrones.Add(drone);
        }

        EnsureDroneInstances(Mathf.Clamp(ownedDroneCount, 1, MaxDroneCount));
        AssignDroneIdentities();
        if (player != null)
        {
            foreach (DroneController drone in cachedDrones)
                drone.SetBaseMoveSpeed(player.BaseMoveSpeed);
        }

        ownedDroneCount = cachedDrones.Count == 0 ? 0 : Mathf.Clamp(ownedDroneCount, 1, cachedDrones.Count);
        for (int index = 0; index < cachedDrones.Count; index++)
            cachedDrones[index].gameObject.SetActive(index < ownedDroneCount);
        Debug.Log($"드론 {cachedDrones.Count}대 등록, {ownedDroneCount}대 보유 상태입니다.");
    }

    public void SetUnlockedFarmPlotCount(int count)
    {
        ApplyUnlockedFarmPlotCount(count);
        FarmSaveService.Instance?.RequestCriticalSave();
    }

    public void SetOwnedDroneCount(int count)
    {
        ApplyOwnedDroneCount(count);
        FarmSaveService.Instance?.RequestCriticalSave();
    }

    public void RestoreOwnership(int unlockedFarmPlots, int ownedDrones)
    {
        ApplyUnlockedFarmPlotCount(unlockedFarmPlots);
        ApplyOwnedDroneCount(ownedDrones);
    }

    private void ApplyUnlockedFarmPlotCount(int count)
    {
        if (cachedFarmPlots.Count == 0)
        {
            unlockedFarmPlotCount = 0;
            FarmPlotOwnershipChanged?.Invoke();
            return;
        }

        unlockedFarmPlotCount = Mathf.Clamp(count, 1, Mathf.Min(MaxFarmPlotCount, cachedFarmPlots.Count));
        for (int index = 0; index < cachedFarmPlots.Count; index++)
            cachedFarmPlots[index].gameObject.SetActive(index < unlockedFarmPlotCount);
        FarmPlotOwnershipChanged?.Invoke();
    }

    public bool IsFarmPlotUnlocked(FarmPlotController farmPlot)
    {
        int index = cachedFarmPlots.IndexOf(farmPlot);
        return index >= 0 && index < unlockedFarmPlotCount;
    }

    private void ApplyOwnedDroneCount(int count)
    {
        int requestedCount = Mathf.Clamp(count, 1, MaxDroneCount);
        EnsureDroneInstances(requestedCount);
        AssignDroneIdentities();
        if (cachedDrones.Count == 0)
        {
            ownedDroneCount = 0;
            return;
        }

        ownedDroneCount = Mathf.Clamp(requestedCount, 1, cachedDrones.Count);
        for (int index = 0; index < cachedDrones.Count; index++)
            cachedDrones[index].gameObject.SetActive(index < ownedDroneCount);
    }

    private void EnsureDroneInstances(int requiredCount)
    {
        if (dronePrefab == null)
            return;
        while (cachedDrones.Count < requiredCount && cachedDrones.Count < MaxDroneCount)
        {
            Vector3 spawnPosition = droneSpawnPoint == null ? transform.position : droneSpawnPoint.position;
            DroneController drone = Instantiate(dronePrefab, spawnPosition, Quaternion.identity, droneParent);
            if (player != null)
                drone.SetBaseMoveSpeed(player.BaseMoveSpeed);
            cachedDrones.Add(drone);
        }
    }

    private void AssignDroneIdentities()
    {
        for (int index = 0; index < cachedDrones.Count; index++)
        {
            int droneNumber = index + 1;
            DroneController drone = cachedDrones[index];
            drone.name = $"Drone_{droneNumber}";
            drone.SetPersistentId($"drone_{droneNumber}");
        }
    }

    public IEnumerable<IFarmAgent> GetFarmAgents()
    {
        if (player != null)
            yield return player;
        for (int index = 0; index < ownedDroneCount && index < cachedDrones.Count; index++)
            yield return cachedDrones[index];
    }

    public bool TryGetCropDefinition(Enum.CropType cropType, out CropDefinition definition)
    {
        return cropsByType.TryGetValue(cropType, out definition);
    }

    public void CacheCropDefinitions()
    {
        cropsByType.Clear();
        foreach (var definition in cropDefinitions)
            if (definition != null && !cropsByType.TryAdd(definition.CropType, definition))
                Debug.LogError("Duplicate crop in GameManager: " + definition.CropType, this);
    }
}
