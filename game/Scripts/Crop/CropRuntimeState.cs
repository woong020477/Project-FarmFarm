// 역할: 개별 작물의 경과 시간과 성장 단계, 수확 가능 여부를 계산한다.
using UnityEngine;

/*
 * CropRuntimeState는 런타임에서 작물의 성장 상태를 관리하는 클래스이다.
 * CropDefinition을 기반으로 성장 단계, 성장 시간, 물을 준 여부 등을 추적한다.
 */
public sealed class CropRuntimeState
{
    private readonly CropDefinition definition;
    private readonly float requiredGrowthSeconds;
    private float elapsedGrowthSeconds;
    private int currentStageIndex;
    public CropDefinition Definition => definition;
    public bool IsHarvestable => currentStageIndex >= definition.MatureStageIndex;
    public int CurrentStageIndex => currentStageIndex;
    public Sprite CurrentSprite => definition.GetStageSprite(currentStageIndex);
    public float RequiredGrowthSeconds => requiredGrowthSeconds;
    public float ElapsedGrowthSeconds => elapsedGrowthSeconds;

    public CropRuntimeState(CropDefinition definition, float randomGrowthMultiplier)
    {
        this.definition = definition;
        requiredGrowthSeconds = Mathf.Max(0.01f, definition.BaseGrowthMinutes * 60f * randomGrowthMultiplier);
        elapsedGrowthSeconds = 0f;
        currentStageIndex = 0;
    }

    public CropRuntimeState(CropDefinition definition, float requiredGrowthSeconds, float elapsedGrowthSeconds)
    {
        this.definition = definition;
        this.requiredGrowthSeconds = Mathf.Max(0.01f, requiredGrowthSeconds);
        this.elapsedGrowthSeconds = Mathf.Clamp(elapsedGrowthSeconds, 0f, this.requiredGrowthSeconds);
        currentStageIndex = CalculateStageIndex();
    }

    public bool Tick(float deltaTime, bool isSoilWet, float growthSpeedMultiplier = 1f)
    {
        if (!isSoilWet || IsHarvestable)
            return false;
        int previousStageIndex = currentStageIndex;
        float growthDeltaTime = deltaTime * Mathf.Max(0f, growthSpeedMultiplier);
        elapsedGrowthSeconds = Mathf.Min(elapsedGrowthSeconds + growthDeltaTime, requiredGrowthSeconds);
        currentStageIndex = CalculateStageIndex();
        return currentStageIndex != previousStageIndex;
    }

    private int CalculateStageIndex()
    {
        if (elapsedGrowthSeconds >= requiredGrowthSeconds)
            return definition.MatureStageIndex;
        float progress = elapsedGrowthSeconds / requiredGrowthSeconds;
        int nonMatureStageCount = definition.GrowthSpriteCount + 1;
        return Mathf.Min(Mathf.FloorToInt(progress * nonMatureStageCount), definition.GrowthSpriteCount);
    }
}
