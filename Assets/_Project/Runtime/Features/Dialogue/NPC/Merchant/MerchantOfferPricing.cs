using UnityEngine;

/// <summary>Assigns one persistent special offer per stock and prices from the original amount.</summary>
public static class MerchantOfferPricing
{
    public static int Calculate(int originalPrice, float globalDiscount, int specialDiscountPercent)
    {
        return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, originalPrice) *
            (1f - Mathf.Clamp01(globalDiscount)) * (1f - Mathf.Clamp(specialDiscountPercent, 0, 100) / 100f)));
    }

    public static void AssignSpecialOffer(MerchantRuntimeState state, int visibleSlots, int minimumPercent, int maximumPercent)
    {
        if (state?.slots == null || state.specialOfferAssigned || minimumPercent <= 0) return;
        int count = Mathf.Min(Mathf.Max(0, visibleSlots), state.slots.Count);
        int candidates = 0;
        for (int i = 0; i < count; i++)
        {
            var entry = state.slots[i];
            if (entry == null) continue;
            if (entry.specialDiscountPercent > 0) { state.specialOfferAssigned = true; return; }
            if (entry.HasItem && !entry.isSold) candidates++;
        }
        state.specialOfferAssigned = true;
        if (candidates == 0) return;
        int selected = Random.Range(0, candidates);
        for (int i = 0; i < count; i++)
        {
            var entry = state.slots[i];
            if (entry == null || !entry.HasItem || entry.isSold || selected-- != 0) continue;
            int minimum = Mathf.Clamp(minimumPercent, 1, 100);
            entry.specialDiscountPercent = Random.Range(minimum, Mathf.Clamp(maximumPercent, minimum, 100) + 1);
            return;
        }
    }
}
