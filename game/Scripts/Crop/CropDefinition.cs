// 역할: 작물별 스프라이트·성장 시간·수확량·가격의 공유 설정 원본.
using Enum;
using UnityEngine;

/*
 * CropDefinition은 작물의 표시 데이터와 서버에 배포할 기본 농사 수치를 정의하는 ScriptableObject이다.
 * Sprite를 데이터에 저장하고, FarmPlotController가 Sprite마다 런타임 Tile을 하나만 생성해서 재사용하도록 구성했다.
 * 성장시간과 수확량은 클라이언트 농사 상태에 사용하고, 에디터 내보내기 도구가 서버의 작물 설정에도 자동 반영한다.
 */
[CreateAssetMenu(fileName = "CropDefinition", menuName = "Farm/Crop Definition")]
public class CropDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private CropType cropType;
    [SerializeField]
    private string displayName;
    [Header("Sprites")]
    [Tooltip("시장과 거래 패널에 표시할 작물 이미지")]
    [SerializeField]
    private Sprite marketSprite;
    [Tooltip("씨앗 상태 즉, 스프라이트의 0번 인덱스")]
    [SerializeField]
    private Sprite seedSprite;
    [Tooltip("0번 인덱스 이후 성장중인 상태 / 1~ N-1번 인덱스")]
    [SerializeField]
    private Sprite[] growthSprites;
    [Tooltip("N-1번 인덱스 이후 성숙 상태 / N번 인덱스")]
    [SerializeField]
    private Sprite matureSprite;
    [Tooltip("다 자란 작물을 옮길 때 보여줄 스프라이트")]
    [SerializeField]
    private Sprite carrySprites;
    [Header("Growth")]
    [Min(0.01f)]
    [SerializeField]
    private float baseGrowthMinutes = 1f;
    [Header("Amount")]
    [Tooltip("수확 시 획득하게되는 기본 수량 값")]
    [SerializeField]
    private long harvestAmount;
    [Header("Price")]
    [Min(1)]
    [SerializeField]
    private int basePrice = 10;
    public int BasePrice => basePrice;

    [Header("Server")]
    [Tooltip("서버에서 작물을 구분하는 고유 ID. 비워두면 CropType에 맞는 기본 ID를 사용합니다.")]
    [SerializeField]
    private string serverId;
    public string ServerId => string.IsNullOrWhiteSpace(serverId) ? GetDefaultServerId(cropType) : serverId.Trim();
    public CropType CropType => cropType;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? cropType.ToString() : displayName;
    public Sprite MarketSprite => marketSprite != null ? marketSprite : carrySprites;
    public float BaseGrowthMinutes => baseGrowthMinutes;
    public float MinGrowthSeconds => baseGrowthMinutes * 60f * 0.75f;
    public float MaxGrowthSeconds => baseGrowthMinutes * 60f * 3f;
    public int GrowthSpriteCount => growthSprites == null ? 0 : growthSprites.Length;
    public int MatureStageIndex => GrowthSpriteCount + 1;
    public Sprite CarrySprite => carrySprites;
    public long HarvestAmount => harvestAmount;

    public Sprite GetStageSprite(int stageIndex)
    {
        if (stageIndex <= 0)
            return seedSprite;
        if (stageIndex <= GrowthSpriteCount)
            return growthSprites[stageIndex - 1];
        return matureSprite;
    }

    public static string GetDefaultServerId(CropType type)
    {
        return type switch
        {
            CropType.Carrot => "crop_carrot",
            CropType.Tomato => "crop_tomato",
            CropType.ChiliPepper => "crop_chili_pepper",
            CropType.PricklyPear => "crop_prickly_pear",
            _ => "crop_" + type.ToString().ToLowerInvariant()};
    }
}
