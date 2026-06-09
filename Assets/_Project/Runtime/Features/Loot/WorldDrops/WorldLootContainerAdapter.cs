using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fixed-slot container for nearby world pickups.
/// - SlotCount is fixed (set in constructor)
/// - Each slot maps to a WorldItemPickup2D instance (or null)
/// - Can only accept null placements (so UI cannot drag items INTO loot slots)
/// - When a loot item is moved out, setting that slot to null destroys the world object.
/// </summary>
public class WorldLootContainerAdapter : IItemContainer, IRelicLevelProvider
{
    public event Action OnChanged;

    private readonly Transform owner;
    private readonly float radius;
    private readonly int slotCount;

    private readonly WorldItemPickup2D[] slots;

    public WorldLootContainerAdapter(Transform owner, float radius, int slotCount)
    {
        this.owner = owner;
        this.radius = Mathf.Max(0f, radius);
        this.slotCount = Mathf.Max(0, slotCount);

        slots = new WorldItemPickup2D[this.slotCount];
        RefreshFromWorld();
    }

    public int SlotCount => slotCount;

    public ScriptableObject Get(int index)
    {
        if (index < 0 || index >= slotCount) return null;
        var w = slots[index];
        return w != null ? w.Item : null;
    }

    public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
    {
        if (index < 0 || index >= slotCount) return false;

        // Loot slots are read-only: only allow placing null back (used by swap-out).
        return item == null;
    }

    public bool TrySet(int index, ScriptableObject item)
    {
        if (index < 0 || index >= slotCount) return false;

        // Only null is supported
        if (item != null) return false;

        var w = slots[index];
        slots[index] = null;

        // Destroy the world object when removed
        if (w != null)
        {
            UnityEngine.Object.Destroy(w.gameObject);
        }

        OnChanged?.Invoke();
        return true;
    }

    public bool TrySwap(int a, int b)
    {
        if (a < 0 || a >= slotCount) return false;
        if (b < 0 || b >= slotCount) return false;
        if (a == b) return true;

        (slots[a], slots[b]) = (slots[b], slots[a]);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Refreshes the fixed slots with nearby world items.
    /// Call this periodically while the inventory UI is open.
    /// </summary>
    public void RefreshFromWorld()
    {
        if (slotCount <= 0)
        {
            OnChanged?.Invoke();
            return;
        }

        // Collect nearby items
        var all = WorldItemRegistry.Items;
        List<WorldItemPickup2D> nearby = new();

        if (owner != null)
        {
            var origin = owner.position;
            float r2 = radius * radius;

            for (int i = 0; i < all.Count; i++)
            {
                var it = all[i];
                if (it == null) continue;
                if (it.Item == null) continue;

                var d2 = (it.transform.position - origin).sqrMagnitude;
                if (d2 <= r2) nearby.Add(it);
            }

            // Sort by distance (closest first)
            nearby.Sort((a, b) =>
            {
                var da = (a.transform.position - origin).sqrMagnitude;
                var db = (b.transform.position - origin).sqrMagnitude;
                return da.CompareTo(db);
            });
        }

        // Fill slots
        for (int i = 0; i < slotCount; i++)
            slots[i] = i < nearby.Count ? nearby[i] : null;

        OnChanged?.Invoke();
    }
    public bool TryGetRelicLevel(int index, out int level)
    {
        level = 0;
        if (index < 0 || index >= slotCount) return false;

        var w = slots[index];
        if (w == null) return false;
        if (w.Item is not RelicDefinition) return false;

        level = Mathf.Max(1, w.RelicLevel);
        return true;
    }

}
