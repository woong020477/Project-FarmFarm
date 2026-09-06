// 역할: 한 농지의 셀 상태·수분·성장·작물 예약을 소유하고 가능한 작업을 판정한다.
using System;
using System.Collections.Generic;
using Enum;
using UnityEngine;
using UnityEngine.Tilemaps;

/*
 * FarmPlotController는 농지의 각 셀을 관리하고, 작물 심기, 물주기, 수확 등의 상호작용을 처리하는 MonoBehaviour이다.
 * CropRuntimeState를 사용하여 각 셀의 작물 상태를 추적하고, Tilemap을 통해 시각적으로 표시한다.
 */
public class FarmPlotController : MonoBehaviour
{
    private enum MoistureVisualState
    {
        Dry,
        HalfWet,
        Wet
    }

    private sealed class FarmCell
    {
        public Vector3Int Position { get; }
        public CropRuntimeState Crop { get; set; }
        public float WetRemainingSeconds { get; private set; }
        public bool IsWet => WetRemainingSeconds > 0f;
        public int Revision { get; set; }
        public bool IsPending { get; set; }
        public MoistureVisualState VisualState { get; set; }

        public FarmCell(Vector3Int position)
        {
            Position = position;
        }

        public bool Water(float durationSeconds)
        {
            if (IsWet)
                return false;
            WetRemainingSeconds = durationSeconds;
            return true;
        }

        public bool TickWetTime(float deltaTime)
        {
            if (!IsWet)
                return false;
            WetRemainingSeconds = Mathf.Max(0f, WetRemainingSeconds - deltaTime);
            return WetRemainingSeconds <= 0f;
        }

        public void RestoreWetTime(float remainingSeconds)
        {
            WetRemainingSeconds = Mathf.Max(0f, remainingSeconds);
        }
    }

    [Header("Identity")]
    [SerializeField]
    private string persistentId = "farm_1";
    [Header("Tilemaps")]
    [SerializeField]
    private Tilemap soilTilemap;
    [SerializeField]
    private Tilemap cropSoilTilemap;
    [Header("Crop Data")]
    [SerializeField, HideInInspector]
    private List<CropDefinition> cropDefinitions = new();
    [SerializeField]
    private CropType selectedCropType = CropType.Carrot;
    [Header("Soil Moisture")]
    [Min(0.1f)]
    [SerializeField]
    private float soilWetDurationSeconds = 60f;
    private Color drySoilColor = Color.white;
    private Color halfWetSoilColor = new(0.84f, 0.84f, 0.84f, 1f);
    private Color wetSoilColor = new(0.68f, 0.68f, 0.68f, 1f);
    private readonly List<FarmCell> orderedCells = new();
    private readonly Dictionary<Vector3Int, FarmCell> cellByPosition = new();
    private readonly Dictionary<CropType, CropDefinition> definitionByType = new();
    private readonly Dictionary<Sprite, Tile> runtimeTiles = new();
    private BoundsInt farmCellBounds;
    private bool hasFarmCellBounds;
    private CropType activeCropType;
    private CropType observedCropType;
    private CropType? reservedCropType;
    private int harvestableCropCount;
    public CropType ActiveCropType => activeCropType;
    public CropType? ReservedCropType => reservedCropType;
    public bool HasReservation => reservedCropType.HasValue;
    public string PersistentId => persistentId;
    public CropType DisplayCropType => reservedCropType ?? activeCropType;
    public IReadOnlyList<CropDefinition> CropDefinitions => GameManager.Instance != null ? GameManager.Instance.CropDefinitions : cropDefinitions;
    public Vector3 FocusPosition => GetFarmWorldBounds().center;

    public event Action SelectionChanged;
    public event Action Harvested;
    private void Awake()
    {
        RegisterDefinitions();
        RegisterFarmCells();
        activeCropType = selectedCropType;
        observedCropType = selectedCropType;
    }

    private void Update()
    {
        DetectInspectorCropSelection();
        TickCrops(Time.deltaTime);
    }

    private void OnDestroy()
    {
        foreach (Tile tile in runtimeTiles.Values)
        {
            if (tile != null)
                Destroy(tile);
        }

        runtimeTiles.Clear();
    }

    // 상호작용 요청을 처리한다. 수확, 물주기, 심기 순으로 시도하며, 성공하면 즉시 반환한다.
    public void InteractAt(Vector3 worldPosition, CropCarrier cropCarrier)
    {
        Vector3Int cellPosition = soilTilemap.WorldToCell(worldPosition);
        if (TryWaterAt(cellPosition))
            return;
        if (TryPlantAt(cellPosition))
            return;
        TryHarvestAt(cellPosition, cropCarrier);
    }

    // CropType 변경 요청을 처리한다. 예약된 작물이 있으면 적용을 지연시키고, 예약된 작물이 없으면 즉시 적용한다.
    public void RequestCropChange(CropType requestedCropType)
    {
        if (!definitionByType.ContainsKey(requestedCropType))
        {
            Debug.LogWarning($"{requestedCropType} 작물 데이터가 등록되지 않았습니다.");
            return;
        }

        if (requestedCropType == activeCropType)
        {
            reservedCropType = null;
            SelectionChanged?.Invoke();
            return;
        }

        if (harvestableCropCount > 0)
        {
            reservedCropType = requestedCropType;
            Debug.Log($"{requestedCropType} 작물이 예약되었습니다.");
            SelectionChanged?.Invoke();
            return;
        }

        ApplyCropChange(requestedCropType);
    }

    // CropDefinition을 CropType 기준으로 등록한다. 중복 등록 시 경고 로그를 출력한다.
    private void RegisterDefinitions()
    {
        definitionByType.Clear();
        foreach (CropDefinition definition in CropDefinitions)
        {
            if (definition == null)
                continue;
            if (!definitionByType.TryAdd(definition.CropType, definition))
                Debug.LogWarning($"{definition.CropType} 작물 데이터가 중복 등록되었습니다.");
        }
    }

    // Soil Tilemap에 실제로 그려진 타일을 농사 가능한 셀로 등록한다.
    private void RegisterFarmCells()
    {
        orderedCells.Clear();
        cellByPosition.Clear();
        if (soilTilemap == null)
        {
            Debug.LogError($"{name}에 Soil Tilemap이 연결되지 않았습니다.");
            return;
        }

        foreach (Vector3Int position in soilTilemap.cellBounds.allPositionsWithin)
        {
            TileBase soilTile = soilTilemap.GetTile(position);
            if (soilTile == null)
                continue;
            FarmCell cell = new(position);
            orderedCells.Add(cell);
            cellByPosition.Add(position, cell);
            TileFlags tileFlags = soilTilemap.GetTileFlags(position);
            soilTilemap.SetTileFlags(position, tileFlags & ~TileFlags.LockColor);
            RefreshSoilColor(cell, true);
        }

        orderedCells.Sort(CompareCellPosition);
        CacheFarmCellBounds();
        Debug.Log($"농지 셀 {orderedCells.Count}개가 등록되었습니다.");
    }

    // 팜 셀을 Y좌표 기준으로 내림차순 정렬하고, Y좌표가 같으면 X좌표 기준으로 오름차순 정렬한다.
    private static int CompareCellPosition(FarmCell left, FarmCell right)
    {
        int yComparison = left.Position.y.CompareTo(right.Position.y);
        if (yComparison != 0)
            return yComparison;
        return left.Position.x.CompareTo(right.Position.x);
    }

    // 선택된 CropType이 변경되었는지 감지하고, 변경되었다면 RequestCropChange를 호출한다.
    private void DetectInspectorCropSelection()
    {
        if (selectedCropType == observedCropType)
            return;
        observedCropType = selectedCropType;
        RequestCropChange(selectedCropType);
    }

    // 각 팜 셀의 CropRuntimeState를 업데이트하고, 성장 단계가 변경되면 cropSoilTilemap의 타일을 갱신한다. 또한, 수확 가능한 작물 수를 추적한다.
    private void TickCrops(float deltaTime)
    {
        foreach (FarmCell cell in orderedCells)
        {
            cell.TickWetTime(deltaTime);
            RefreshSoilColor(cell);
            CropRuntimeState crop = cell.Crop;
            if (crop == null)
                continue;
            bool wasHarvestable = crop.IsHarvestable;
            GameEvents gameEvents = GameEvents.Instance;
            float growthSpeedMultiplier = gameEvents == null ? 1f : gameEvents.GetMultiplier(StatType.CropGrowth, StatTargets.Crop(crop.Definition.CropType));
            bool stageChanged = crop.Tick(deltaTime, cell.IsWet, growthSpeedMultiplier);
            if (stageChanged)
                cropSoilTilemap.SetTile(cell.Position, GetRuntimeTile(crop.CurrentSprite));
            if (!wasHarvestable && crop.IsHarvestable)
            {
                harvestableCropCount++;
                Debug.Log($"{cell.Position}의 {crop.Definition.CropType} 작물이 성장 완료되었습니다.");
            }
        }
    }

    // 지정된 위치에 작물을 심으려고 시도한다. 해당 위치에 이미 작물이 있거나, CropDefinition이 없으면 false를 반환한다. 심기 성공 시, cropSoilTilemap에 작물의 현재 Sprite를 표시한다.
    private bool TryPlantAt(Vector3Int position)
    {
        if (reservedCropType.HasValue)
        {
            Debug.Log($"다음 작물 {reservedCropType.Value} 적용을 기다리는 중입니다.");
            return false;
        }

        if (!cellByPosition.TryGetValue(position, out FarmCell cell))
            return false;
        if (!cell.IsWet || cell.Crop != null)
            return false;
        if (!definitionByType.TryGetValue(activeCropType, out CropDefinition definition))
        {
            Debug.LogWarning($"{activeCropType} 작물 데이터가 없습니다.");
            return false;
        }

        return PerformLocalWork(FarmWorkType.Plant, cell, null);
    }

    private void RefreshSoilColor(FarmCell cell, bool force = false)
    {
        if (cell == null || soilTilemap == null)
            return;
        MoistureVisualState nextState;
        if (!cell.IsWet)
            nextState = MoistureVisualState.Dry;
        else if (cell.WetRemainingSeconds <= soilWetDurationSeconds * 0.5f)
            nextState = MoistureVisualState.HalfWet;
        else
            nextState = MoistureVisualState.Wet;
        if (!force && cell.VisualState == nextState)
            return;
        cell.VisualState = nextState;
        Color color = nextState switch
        {
            MoistureVisualState.Wet => wetSoilColor,
            MoistureVisualState.HalfWet => halfWetSoilColor,
            _ => drySoilColor
        };
        soilTilemap.SetColor(cell.Position, color);
    }

    // 지정된 위치에 물을 주고 Soil Tilemap의 해당 셀 색상을 갱신한다.
    private bool TryWaterAt(Vector3Int position)
    {
        if (!cellByPosition.TryGetValue(position, out FarmCell cell))
            return false;
        if (cell.IsWet)
            return false;
        return PerformLocalWork(FarmWorkType.Water, cell, null);
    }

    // 수확 시도. 수확 가능 상태가 아니거나, 해당 위치에 작물이 없으면 false를 반환한다. 수확 성공 시, harvestableCropCount를 감소시키고, 예약된 작물이 있으면 적용한다.
    private bool TryHarvestAt(Vector3Int position, CropCarrier cropCarrier)
    {
        if (!cellByPosition.TryGetValue(position, out FarmCell cell))
            return false;
        if (cell.Crop == null || !cell.Crop.IsHarvestable)
            return false;
        if (cropCarrier == null || !cropCarrier.CanCarry)
            return false;
        return PerformLocalWork(FarmWorkType.Harvest, cell, cropCarrier);
    }

    private bool PerformLocalWork(FarmWorkType workType, FarmCell cell, CropCarrier cropCarrier)
    {
        if (cell == null || cell.IsPending)
            return false;
        if (workType == FarmWorkType.Water)
        {
            if (!cell.Water(soilWetDurationSeconds))
                return false;
            cell.Revision++;
            RefreshSoilColor(cell);
            Debug.Log($"{cell.Position} 토양에 물을 주었습니다. 지속시간: {soilWetDurationSeconds:0}초");
            return true;
        }

        if (workType == FarmWorkType.Plant)
        {
            if (!definitionByType.TryGetValue(activeCropType, out CropDefinition definition))
                return false;
            cell.Crop = new CropRuntimeState(definition, UnityEngine.Random.Range(0.75f, 3f));
            cell.Revision++;
            cropSoilTilemap.SetTile(cell.Position, GetRuntimeTile(cell.Crop.CurrentSprite));
            Debug.Log($"{cell.Position}에 {activeCropType} 씨앗을 심었습니다.");
            return true;
        }

        if (workType == FarmWorkType.Harvest)
        {
            CropDefinition harvestedCrop = cell.Crop.Definition;
            long harvestAmount = Math.Max(1L, harvestedCrop.HarvestAmount);
            string harvestId = Guid.NewGuid().ToString("N");
            if (cropCarrier == null || !cropCarrier.TryLoad(harvestedCrop, harvestAmount, harvestId))
                return false;
            cell.Crop = null;
            cell.Revision++;
            cropSoilTilemap.SetTile(cell.Position, null);
            harvestableCropCount = Mathf.Max(0, harvestableCropCount - 1);
            Harvested?.Invoke();
            Debug.Log($"{cell.Position}에서 {harvestedCrop.CropType} 작물을 {harvestAmount}개 수확했습니다.");
            if (harvestableCropCount == 0 && reservedCropType.HasValue)
                ApplyCropChange(reservedCropType.Value);
            return true;
        }

        return false;
    }

    // CropType 변경을 적용한다. activeCropType을 업데이트하고 reservedCropType을 초기화한다.
    private void ApplyCropChange(CropType cropType)
    {
        activeCropType = cropType;
        reservedCropType = null;
        SelectionChanged?.Invoke();
        Debug.Log($"심을 작물이 {activeCropType}로 변경되었습니다.");
    }

    // Sprite를 기반으로 런타임 Tile을 생성하거나 캐시에서 가져온다. 이미 생성된 Tile이 있으면 재사용한다.
    private TileBase GetRuntimeTile(Sprite sprite)
    {
        if (sprite == null)
            return null;
        if (runtimeTiles.TryGetValue(sprite, out Tile cachedTile))
            return cachedTile;
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = $"RuntimeTile_{sprite.name}";
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        runtimeTiles.Add(sprite, tile);
        return tile;
    }

    public bool ContainsWorldPosition(Vector3 worldPosition)
    {
        Vector3Int cellPosition = soilTilemap.WorldToCell(worldPosition);
        return cellByPosition.ContainsKey(cellPosition);
    }

    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        return soilTilemap.WorldToCell(worldPosition);
    }

    public Vector3 GetCellCenterWorld(Vector3Int cellPosition)
    {
        return soilTilemap.GetCellCenterWorld(cellPosition);
    }

    public Bounds GetFarmWorldBounds()
    {
        if (!hasFarmCellBounds)
            CacheFarmCellBoundsFromSoilTiles();
        if (!hasFarmCellBounds || soilTilemap == null)
            return new Bounds(transform.position, Vector3.zero);
        Vector3Int min = farmCellBounds.min;
        Vector3Int max = farmCellBounds.max;
        Vector3 bottomLeft = soilTilemap.CellToWorld(min);
        Vector3 bottomRight = soilTilemap.CellToWorld(new Vector3Int(max.x, min.y, min.z));
        Vector3 topLeft = soilTilemap.CellToWorld(new Vector3Int(min.x, max.y, min.z));
        Vector3 topRight = soilTilemap.CellToWorld(new Vector3Int(max.x, max.y, min.z));
        Bounds worldBounds = new(bottomLeft, Vector3.zero);
        worldBounds.Encapsulate(bottomRight);
        worldBounds.Encapsulate(topLeft);
        worldBounds.Encapsulate(topRight);
        worldBounds.size = new Vector3(worldBounds.size.x, worldBounds.size.y, 0f);
        return worldBounds;
    }

    private void CacheFarmCellBounds()
    {
        if (orderedCells.Count == 0)
        {
            hasFarmCellBounds = false;
            farmCellBounds = default;
            return;
        }

        Vector3Int min = orderedCells[0].Position;
        Vector3Int max = orderedCells[0].Position;
        for (int index = 1; index < orderedCells.Count; index++)
        {
            min = Vector3Int.Min(min, orderedCells[index].Position);
            max = Vector3Int.Max(max, orderedCells[index].Position);
        }

        farmCellBounds = new BoundsInt(min, max - min + Vector3Int.one);
        hasFarmCellBounds = true;
    }

    private void CacheFarmCellBoundsFromSoilTiles()
    {
        if (soilTilemap == null)
            return;
        List<Vector3Int> positions = new();
        foreach (Vector3Int position in soilTilemap.cellBounds.allPositionsWithin)
        {
            if (soilTilemap.HasTile(position))
                positions.Add(position);
        }

        hasFarmCellBounds = TryCalculatePositionBounds(positions, out farmCellBounds);
    }

    private static bool TryCalculatePositionBounds(IReadOnlyList<Vector3Int> positions, out BoundsInt bounds)
    {
        bounds = default;
        if (positions == null || positions.Count == 0)
            return false;
        Vector3Int min = positions[0];
        Vector3Int max = positions[0];
        for (int index = 1; index < positions.Count; index++)
        {
            min = Vector3Int.Min(min, positions[index]);
            max = Vector3Int.Max(max, positions[index]);
        }

        bounds = new BoundsInt(min, max - min + Vector3Int.one);
        return true;
    }

    public bool IsWorkAvailable(FarmWorkType workType, Vector3Int position)
    {
        if (!cellByPosition.TryGetValue(position, out FarmCell cell))
            return false;
        return workType switch
        {
            FarmWorkType.Harvest => !cell.IsPending && cell.Crop != null && cell.Crop.IsHarvestable,
            FarmWorkType.Water => !cell.IsPending && !cell.IsWet,
            FarmWorkType.Plant => !cell.IsPending && !reservedCropType.HasValue && cell.IsWet && cell.Crop == null,
            _ => false
        };
    }

    public bool TryGetClosestWorkCell(Vector3 worldPosition, FarmWorkType workType, out Vector3Int targetPosition, out float targetDistance)
    {
        return TryGetClosestWorkCell(worldPosition, workType, false, out targetPosition, out targetDistance);
    }

    public bool TryGetClosestWorkCell(Vector3 worldPosition, FarmWorkType workType, bool allowDiagonalMovement, out Vector3Int targetPosition, out float targetDistance)
    {
        targetPosition = default;
        targetDistance = float.MaxValue;
        bool found = false;
        foreach (FarmCell cell in orderedCells)
        {
            if (!IsWorkAvailable(workType, cell.Position))
                continue;
            Vector3 cellWorldPosition = GetCellCenterWorld(cell.Position);
            float distance = allowDiagonalMovement ? Vector2.Distance(cellWorldPosition, worldPosition) : Mathf.Abs(cellWorldPosition.x - worldPosition.x) + Mathf.Abs(cellWorldPosition.y - worldPosition.y);
            if (distance >= targetDistance)
                continue;
            targetPosition = cell.Position;
            targetDistance = distance;
            found = true;
        }

        return found;
    }

    public bool TryPerformWork(FarmWorkType workType, Vector3Int position, CropCarrier cropCarrier)
    {
        return workType switch
        {
            FarmWorkType.Harvest => TryHarvestAt(position, cropCarrier),
            FarmWorkType.Water => TryWaterAt(position),
            FarmWorkType.Plant => TryPlantAt(position),
            _ => false
        };
    }

    public bool TryGetCropDefinition(CropType cropType, out CropDefinition definition)
    {
        return definitionByType.TryGetValue(cropType, out definition);
    }

    public FarmPlotSaveData CaptureSaveData()
    {
        FarmPlotSaveData saveData = new()
        {
            persistentId = persistentId,
            activeCropType = activeCropType,
            hasReservedCrop = reservedCropType.HasValue,
            reservedCropType = reservedCropType.GetValueOrDefault()
        };
        foreach (FarmCell cell in orderedCells)
        {
            if (!cell.IsWet && cell.Crop == null)
                continue;
            FarmCellSaveData cellSaveData = new()
            {
                positionX = cell.Position.x,
                positionY = cell.Position.y,
                positionZ = cell.Position.z,
                wetRemainingSeconds = cell.WetRemainingSeconds,
                hasCrop = cell.Crop != null,
                revision = cell.Revision
            };
            if (cell.Crop != null)
            {
                cellSaveData.cropType = cell.Crop.Definition.CropType;
                cellSaveData.requiredGrowthSeconds = cell.Crop.RequiredGrowthSeconds;
                cellSaveData.elapsedGrowthSeconds = cell.Crop.ElapsedGrowthSeconds;
            }

            saveData.cells.Add(cellSaveData);
        }

        return saveData;
    }

    public void RestoreSaveData(FarmPlotSaveData saveData)
    {
        if (saveData == null || saveData.persistentId != persistentId)
            return;
        harvestableCropCount = 0;
        foreach (FarmCell cell in orderedCells)
        {
            cell.Crop = null;
            cell.IsPending = false;
            cell.Revision = 0;
            cell.RestoreWetTime(0f);
            RefreshSoilColor(cell, true);
            cropSoilTilemap.SetTile(cell.Position, null);
        }

        activeCropType = definitionByType.ContainsKey(saveData.activeCropType) ? saveData.activeCropType : selectedCropType;
        selectedCropType = activeCropType;
        observedCropType = activeCropType;
        reservedCropType = saveData.hasReservedCrop && definitionByType.ContainsKey(saveData.reservedCropType) ? saveData.reservedCropType : null;
        if (saveData.cells != null)
        {
            foreach (FarmCellSaveData cellSaveData in saveData.cells)
            {
                Vector3Int position = new(cellSaveData.positionX, cellSaveData.positionY, cellSaveData.positionZ);
                if (!cellByPosition.TryGetValue(position, out FarmCell cell))
                    continue;
                cell.RestoreWetTime(cellSaveData.wetRemainingSeconds);
                cell.Revision = Mathf.Max(0, cellSaveData.revision);
                RefreshSoilColor(cell, true);
                if (!cellSaveData.hasCrop || !definitionByType.TryGetValue(cellSaveData.cropType, out CropDefinition definition))
                    continue;
                cell.Crop = new CropRuntimeState(definition, cellSaveData.requiredGrowthSeconds, cellSaveData.elapsedGrowthSeconds);
                cropSoilTilemap.SetTile(position, GetRuntimeTile(cell.Crop.CurrentSprite));
                if (cell.Crop.IsHarvestable)
                    harvestableCropCount++;
            }
        }

        SelectionChanged?.Invoke();
    }

    public void RestoreAuthoritativeCells(IReadOnlyList<FarmCellSaveData> cells)
    {
        FarmPlotSaveData state = new()
        {
            persistentId = persistentId,
            activeCropType = activeCropType,
            hasReservedCrop = reservedCropType.HasValue,
            reservedCropType = reservedCropType.GetValueOrDefault(),
            cells = cells == null ? new List<FarmCellSaveData>() : new List<FarmCellSaveData>(cells)
        };
        RestoreSaveData(state);
    }
}
