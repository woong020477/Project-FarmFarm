// 역할: 한 운반 주체가 들고 있는 작물·수량·입고 ID와 부착 스프라이트를 관리한다.
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class CropCarrier : MonoBehaviour
{
    private SpriteRenderer carrySpriteRenderer;
    private CropDefinition carriedCrop;
    private long carriedAmount;
    private string harvestId;
    private string ownerId;
    public bool IsCarrying => carriedCrop != null;
    public bool CanCarry => !IsCarrying;
    public CropDefinition CarriedCrop => carriedCrop;
    public long CarriedAmount => carriedAmount;
    public string HarvestId => harvestId;
    public string OwnerId => ownerId;

    private void Awake()
    {
        carrySpriteRenderer = GetComponent<SpriteRenderer>();
        RefreshVisual();
    }

    public bool TryLoad(CropDefinition crop)
    {
        if (crop == null)
        {
            Debug.LogWarning("운반할 작물 데이터가 없습니다.");
            return false;
        }

        return TryLoad(crop, crop.HarvestAmount);
    }

    public bool TryLoad(CropDefinition crop, long amount)
    {
        return TryLoad(crop, amount, null);
    }

    public bool TryLoad(CropDefinition crop, long amount, string carriedHarvestId)
    {
        if (IsCarrying)
        {
            Debug.Log($"이미 {carriedCrop.CropType} 작물을 운반하고 있습니다.");
            return false;
        }

        if (crop == null)
        {
            Debug.LogWarning("운반할 작물 데이터가 없습니다.");
            return false;
        }

        if (crop.CarrySprite == null)
        {
            Debug.LogWarning($"{crop.CropType} 작물의 운반 스프라이트가 없습니다.");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogWarning($"{crop.CropType} 작물의 수확량이 올바르지 않습니다: {amount}");
            return false;
        }

        carriedCrop = crop;
        carriedAmount = amount;
        harvestId = carriedHarvestId;
        RefreshVisual();
        Debug.Log($"{carriedCrop.CropType} 작물 {carriedAmount}개를 운반합니다.");
        return true;
    }

    public bool TryGetCarriedCrop(out CropDefinition crop, out long amount)
    {
        crop = carriedCrop;
        amount = carriedAmount;
        return crop != null && amount > 0;
    }

    public bool TryGetDepositInfo(out CropDefinition crop, out long amount, out string carriedHarvestId)
    {
        bool hasCrop = TryGetCarriedCrop(out crop, out amount);
        carriedHarvestId = harvestId;
        return hasCrop && !string.IsNullOrWhiteSpace(carriedHarvestId);
    }

    public bool TryUnload(CropDefinition expectedCrop, long expectedAmount)
    {
        if (carriedCrop != expectedCrop || carriedAmount != expectedAmount)
            return false;
        return TryUnload(out _, out _);
    }

    public bool TryUnload(out CropDefinition crop, out long amount)
    {
        if (!IsCarrying)
        {
            crop = null;
            amount = 0;
            return false;
        }

        crop = carriedCrop;
        amount = carriedAmount;
        carriedCrop = null;
        carriedAmount = 0;
        harvestId = null;
        RefreshVisual();
        Debug.Log($"{crop.CropType} 작물 {amount}개를 내려놓았습니다.");
        return true;
    }

    public void RestoreState(CropDefinition crop, long amount)
    {
        RestoreState(crop, amount, null);
    }

    public void RestoreState(CropDefinition crop, long amount, string carriedHarvestId)
    {
        carriedCrop = null;
        carriedAmount = 0;
        harvestId = null;
        if (crop != null && amount > 0)
        {
            carriedCrop = crop;
            carriedAmount = amount;
            harvestId = carriedHarvestId;
        }

        RefreshVisual();
    }

    public void SetOwnerId(string id)
    {
        ownerId = string.IsNullOrWhiteSpace(id) ? null : id;
    }

    private void RefreshVisual()
    {
        Sprite carrySprite = carriedCrop == null ? null : carriedCrop.CarrySprite;
        carrySpriteRenderer.sprite = carrySprite;
        carrySpriteRenderer.enabled = carrySprite != null;
    }
}
