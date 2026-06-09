using UnityEngine;

public readonly struct MerchantShopPolicySnapshot
{
    public readonly bool HasDefinition;
    public readonly bool IsAvailable;
    public readonly int VisibleSlotCount;
    public readonly int RefreshLimit;
    public readonly float DiscountRate;
    public readonly MerchantPriceSettings EffectivePriceSettings;

    public MerchantShopPolicySnapshot(
        bool hasDefinition,
        bool isAvailable,
        int visibleSlotCount,
        int refreshLimit,
        float discountRate,
        MerchantPriceSettings effectivePriceSettings)
    {
        HasDefinition = hasDefinition;
        IsAvailable = isAvailable;
        VisibleSlotCount = Mathf.Max(0, visibleSlotCount);
        RefreshLimit = Mathf.Max(0, refreshLimit);
        DiscountRate = Mathf.Clamp01(discountRate);
        EffectivePriceSettings = effectivePriceSettings;
    }
}

public static class MerchantShopPolicy
{
    public static MerchantShopPolicySnapshot Resolve(
        ShopDefinitionSO definition,
        ShopRunModifierDelta modifiers,
        int authoredSlotCount)
    {
        authoredSlotCount = Mathf.Max(0, authoredSlotCount);
        if (definition == null)
            return new MerchantShopPolicySnapshot(false, false, 0, 0, 0f, default);

        bool isAvailable = !definition.RequireShopUpgrade || modifiers.shopEnabled;
        int baseSlotCount = definition.BaseVisibleSlotCount > 0
            ? Mathf.Min(definition.BaseVisibleSlotCount, authoredSlotCount)
            : authoredSlotCount;

        int visibleSlotCount = isAvailable
            ? Mathf.Clamp(baseSlotCount + Mathf.Max(0, modifiers.shopSlotBonus), 0, authoredSlotCount)
            : 0;

        float discountRate = Mathf.Clamp01(modifiers.discountRate);
        MerchantPriceSettings effectivePriceSettings = definition.PriceSettings.WithDiscount(discountRate);

        return new MerchantShopPolicySnapshot(
            true,
            isAvailable,
            visibleSlotCount,
            modifiers.shopRefreshCount,
            discountRate,
            effectivePriceSettings);
    }
}
