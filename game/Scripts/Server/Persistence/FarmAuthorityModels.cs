// 역할: 농지 동기화 명령과 응답의 직렬화 모델. 클라이언트/서버 필드 계약을 유지한다.
using System;
using System.Collections.Generic;
using Enum;

[Serializable]
public sealed class FarmCommandData
{
    public string commandId;
    public string action;
    public string farmId;
    public int positionX;
    public int positionY;
    public int positionZ;
    public int expectedRevision;
    public CropType cropType;
    public string agentId;
}

[Serializable]
public sealed class FarmCommandBatchData
{
    public List<FarmCommandData> commands = new();
}

[Serializable]
public sealed class FarmCommandResultData
{
    public string commandId;
    public bool success;
    public string error;
    public bool hasState;
    public string farmId;
    public int positionX;
    public int positionY;
    public int positionZ;
    public int revision;
    public float wetRemainingSeconds;
    public bool hasCrop;
    public CropType cropType;
    public float requiredGrowthSeconds;
    public float elapsedGrowthSeconds;
    public string harvestId;
    public long harvestAmount;
}

[Serializable]
public sealed class FarmCommandResultList
{
    public List<FarmCommandResultData> results = new();
}

[Serializable]
public sealed class FarmAuthorityPlotState
{
    public string persistentId;
    public List<FarmCellSaveData> cells = new();
}

[Serializable]
public sealed class FarmAuthorityCargoState
{
    public string agentId;
    public string harvestId;
    public CropType cropType;
    public long amount;
}

[Serializable]
public sealed class FarmAuthorityState
{
    public List<FarmAuthorityPlotState> plots = new();
    public List<FarmAuthorityCargoState> cargos = new();
}
