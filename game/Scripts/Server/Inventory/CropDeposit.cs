// 역할: 묶음 입고 요청과 처리 결과의 모델. 입고 ID는 중복 반영 방지에 사용한다.
using System.Collections.Generic;
using Enum;

public readonly struct CropDeposit
{
    public CropType CropType { get; }
    public long Amount { get; }

    public CropDeposit(CropType cropType, long amount)
    {
        CropType = cropType;
        Amount = amount;
    }
}

public sealed class CropDepositRequest
{
    public string harvestId;
    public int cropType;
    public long amount;
    public CropDepositRequest(string harvestId, CropType cropType, long amount)
    {
        this.harvestId = harvestId;
        this.cropType = (int)cropType;
        this.amount = amount;
    }
}

public readonly struct CropDepositBatchResult
{
    public string BatchId { get; }
    public long TotalAmount { get; }
    public bool WasDuplicate { get; }
    public IReadOnlyDictionary<CropType, long> ConfirmedTotals { get; }

    public CropDepositBatchResult(string batchId, long totalAmount, bool wasDuplicate, IReadOnlyDictionary<CropType, long> confirmedTotals)
    {
        BatchId = batchId;
        TotalAmount = totalAmount;
        WasDuplicate = wasDuplicate;
        ConfirmedTotals = confirmedTotals;
    }
}
