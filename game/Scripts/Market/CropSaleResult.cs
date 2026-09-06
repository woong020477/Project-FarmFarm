// 역할: 확인된 판매 수량·금액·쿨다운을 화면에 전달하는 결과 모델.
using Enum;

public readonly struct CropSaleResult
{
    public CropType CropType { get; }
    public long Amount { get; }
    public int UnitPrice { get; }
    public long TotalPrice { get; }
    public long CooldownEndUnixSeconds { get; }

    public CropSaleResult(CropType cropType, long amount, int unitPrice, long totalPrice, long cooldownEndUnixSeconds)
    {
        CropType = cropType;
        Amount = amount;
        UnitPrice = unitPrice;
        TotalPrice = totalPrice;
        CooldownEndUnixSeconds = cooldownEndUnixSeconds;
    }
}
