// 역할: 농장·셀·에이전트 위치·운반물의 저장 모델. 이전 저장 형식과 호환성을 유지한다.
using System;
using System.Collections.Generic;
using Enum;

[Serializable]
public sealed class FarmGameSaveData
{
    public int version = 1;
    public long savedAtUnixSeconds;
    public int unlockedFarmPlotCount = 1;
    public int ownedDroneCount = 1;
    public long playerLevel = 1L;
    public long currentExperience;
    public string playerTitle = "없음";
    public List<FarmAgentSaveData> agents = new();
    public List<FarmPlotSaveData> farmPlots = new();
}

[Serializable]
public sealed class FarmAgentSaveData
{
    public string persistentId;
    public float positionX;
    public float positionY;
    public float positionZ;
    public bool isCarrying;
    public CropType carriedCropType;
    public long carriedAmount;
    public string carriedHarvestId;
}

[Serializable]
public sealed class FarmPlotSaveData
{
    public string persistentId;
    public CropType activeCropType;
    public bool hasReservedCrop;
    public CropType reservedCropType;
    public List<FarmCellSaveData> cells = new();
}

[Serializable]
public sealed class FarmCellSaveData
{
    public int positionX;
    public int positionY;
    public int positionZ;
    public float wetRemainingSeconds;
    public bool hasCrop;
    public CropType cropType;
    public float requiredGrowthSeconds;
    public float elapsedGrowthSeconds;
    public int revision;
}
