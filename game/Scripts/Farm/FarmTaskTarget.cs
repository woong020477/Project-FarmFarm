// 역할: 선택된 농지·셀·작업 종류를 묶는 불변 작업 대상.
using UnityEngine;

public readonly struct FarmTaskTarget
{
    public FarmPlotController FarmPlot { get; }
    public Vector3Int CellPosition { get; }
    public FarmWorkType WorkType { get; }

    public FarmTaskTarget(FarmPlotController farmPlot, Vector3Int cellPosition, FarmWorkType workType)
    {
        FarmPlot = farmPlot;
        CellPosition = cellPosition;
        WorkType = workType;
    }
}
