using System;
using UnityEngine;

/// <summary>
/// Player-owned generic inventory ("bag") that can store both weapons and relics.
/// Internally it reuses <see cref="ChestInventory"/>.
/// </summary>
public class PlayerBackpackInventory : MonoBehaviour
{
    [Header("Bag")]
    [SerializeField] private int capacity = 16;

    [SerializeField] private ChestInventory inventory;

    public event Action OnChanged;

    public int Capacity => inventory != null ? inventory.Capacity : 0;

    public ChestInventory Inventory => inventory;

    private void Awake()
    {
        // If capacity changes in inspector, recreate (this will reset contents).
        if (inventory == null || inventory.Capacity != Mathf.Max(0, capacity))
        {
            inventory = new ChestInventory(Mathf.Max(0, capacity));
        }

        // Bridge event
        inventory.OnChanged += HandleChanged;
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnChanged -= HandleChanged;
    }

    private void HandleChanged() => OnChanged?.Invoke();

    // Convenience wrappers
    public ScriptableObject Get(int index) => inventory != null ? inventory.Get(index) : null;
    public bool Set(int index, ScriptableObject item) => inventory != null && inventory.Set(index, item);
    public bool Swap(int a, int b) => inventory != null && inventory.Swap(a, b);

    public bool TryAdd(ScriptableObject item)
    {
        if (inventory == null || item == null) return false;
        if (!inventory.TryFindEmpty(out int idx)) return false;
        return inventory.Set(idx, item);
    }
}
