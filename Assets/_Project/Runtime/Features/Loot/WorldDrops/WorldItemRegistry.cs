using System.Collections.Generic;

/// <summary>
/// Tracks all active world item pickups.
/// Used by InventoryScreen to show nearby loot while the inventory UI is open.
/// </summary>
public static class WorldItemRegistry
{
    private static readonly List<WorldItemPickup2D> items = new();

    public static IReadOnlyList<WorldItemPickup2D> Items => items;

    public static void Register(WorldItemPickup2D item)
    {
        if (item == null) return;
        if (!items.Contains(item)) items.Add(item);
    }

    public static void Unregister(WorldItemPickup2D item)
    {
        if (item == null) return;
        items.Remove(item);
    }
}
