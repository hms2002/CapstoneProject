using System.Collections.Generic;
using UnityEngine;

internal static class ChestRewardPolicy
{
    public static bool CanRefreshLoot(
        bool isGenerated,
        ChestInventory inventory,
        bool hasLootManager,
        int refreshCountUsed,
        IReadOnlyList<ChestLootSnapshot> refreshGuard)
    {
        if (!isGenerated || inventory == null || !hasLootManager)
            return false;

        int refreshLimit = ResolveRefreshLimit();
        if (refreshCountUsed >= refreshLimit)
            return false;

        return MatchesRefreshGuard(inventory, refreshGuard);
    }

    public static int ResolveRefreshLimit()
    {
        return Mathf.Max(0, ResolveChestModifiers().chestRefreshCount);
    }

    public static int ResolveRemainingRefreshCount(
        bool isGenerated,
        ChestInventory inventory,
        bool hasLootManager,
        int refreshCountUsed,
        IReadOnlyList<ChestLootSnapshot> refreshGuard)
    {
        if (!isGenerated || inventory == null || !hasLootManager)
            return 0;

        int remainingCount = Mathf.Max(0, ResolveRefreshLimit() - Mathf.Max(0, refreshCountUsed));
        if (remainingCount <= 0)
            return 0;

        return MatchesRefreshGuard(inventory, refreshGuard) ? remainingCount : 0;
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

    public static void RecordRefreshGuard(ChestInventory inventory, List<ChestLootSnapshot> refreshGuard)
    {
        if (refreshGuard == null)
            return;

        refreshGuard.Clear();
        if (inventory == null)
            return;

        for (int i = 0; i < inventory.Capacity; i++)
        {
            ScriptableObject item = inventory.Get(i);
            refreshGuard.Add(new ChestLootSnapshot(item, inventory.GetRelicLevelInSlot(i)));
        }
    }

    private static bool MatchesRefreshGuard(
        ChestInventory inventory,
        IReadOnlyList<ChestLootSnapshot> refreshGuard)
    {
        if (inventory == null || refreshGuard == null || refreshGuard.Count != inventory.Capacity)
            return false;

        for (int i = 0; i < inventory.Capacity; i++)
        {
            ChestLootSnapshot snapshot = refreshGuard[i];
            if (inventory.Get(i) != snapshot.Item)
                return false;

            if (inventory.GetRelicLevelInSlot(i) != snapshot.RelicLevel)
                return false;
        }

        return true;
    }

    private static ChestRunModifierDelta ResolveChestModifiers()
    {
        return RunModifierService.CurrentRewardSnapshot.ChestModifiers;
    }
}

internal readonly struct ChestLootSnapshot
{
    public readonly ScriptableObject Item;
    public readonly int RelicLevel;

    public ChestLootSnapshot(ScriptableObject item, int relicLevel)
    {
        Item = item;
        RelicLevel = relicLevel;
    }
}
