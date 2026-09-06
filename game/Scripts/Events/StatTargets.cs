// 역할: 작물 등 능력치 적용 대상을 공통 문자열 식별자로 변환한다.
using Enum;

public static class StatTargets
{
    public const string All = "all";
    public static string Crop(CropType cropType)
    {
        return GameManager.Instance != null && GameManager.Instance.TryGetCropDefinition(cropType, out var definition) ? definition.ServerId : CropDefinition.GetDefaultServerId(cropType);
    }
}
