using System.Collections.Generic;

/// <summary>
/// 책임 : 현재 활성화된 월드 아이템 픽업 목록을 보관해 inventory UI가 근처 loot를 조회할 수 있게 한다.
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
