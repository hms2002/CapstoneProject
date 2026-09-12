using UnityEngine;

internal static class ChestRewardPolicy
{
    public static bool CanRefreshLoot(
        bool isGenerated,
        ChestInventory inventory,
        bool hasLootManager,
        int refreshCountUsed)
    {
        if (!isGenerated || inventory == null || !hasLootManager)
            return false;

        int refreshLimit = ResolveRefreshLimit();
        if (refreshCountUsed >= refreshLimit)
            return false;

        return inventory.AcquiredCount == 0;
    }

    public static int ResolveRefreshLimit()
    {
        return Mathf.Max(0, ResolveChestModifiers().chestRefreshCount);
    }

    public static int ResolveRemainingRefreshCount(
        bool isGenerated,
        ChestInventory inventory,
        bool hasLootManager,
        int refreshCountUsed)
    {
        if (!isGenerated || inventory == null || !hasLootManager)
            return 0;

        int remainingCount = Mathf.Max(0, ResolveRefreshLimit() - Mathf.Max(0, refreshCountUsed));
        if (remainingCount <= 0)
            return 0;

        return inventory.AcquiredCount == 0 ? remainingCount : 0;
    }

    public static int ResolveChestRelicLevel(RelicDefinition relic)
    {
        if (relic == null)
            return 0;

        int level = relic.dropLevel > 0 ? relic.dropLevel : 1;
        ChestRunModifierDelta modifiers = ResolveChestModifiers();

        float chance = Mathf.Clamp01(modifiers.relicLevelBonusChance);
        if (chance > 0f && Random.value < chance)
            level++;

        return relic.ClampLevel(level);
    }

    private static ChestRunModifierDelta ResolveChestModifiers()
    {
        return RunModifierService.CurrentRewardSnapshot.ChestModifiers;
    }
}
