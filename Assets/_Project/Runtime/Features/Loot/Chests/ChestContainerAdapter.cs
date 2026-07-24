using System;
using UnityEngine;

internal sealed class ChestContainerAdapter : IItemContainer, IDisposable, IRelicLevelProvider, IRelicSlotReceiver
{
    private readonly ChestInventory inventory;
    public event Action OnChanged;

    public ChestContainerAdapter(ChestInventory inventory)
    {
        this.inventory = inventory;
        if (this.inventory != null)
            this.inventory.OnChanged += HandleChanged;
    }

    public int SlotCount => inventory != null ? inventory.Capacity : 0;

    public ScriptableObject Get(int index)
    {
        return inventory != null ? inventory.Get(index) : null;
    }

    public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
    {
        return true;
    }

    public bool TrySet(int index, ScriptableObject item)
    {
        return inventory != null && inventory.Set(index, item);
    }

    public bool TrySwap(int a, int b)
    {
        return inventory != null && inventory.Swap(a, b);
    }

    public bool TryGetRelicLevel(int index, out int level)
    {
        level = inventory != null ? inventory.GetRelicLevelInSlot(index) : 0;
        return level > 0;
    }

    public bool TrySetRelicWithLevel(int index, RelicDefinition relic, int level)
    {
        return inventory != null && inventory.SetRelicWithLevel(index, relic, level);
    }

    public void Dispose()
    {
        if (inventory != null)
            inventory.OnChanged -= HandleChanged;
    }

    private void HandleChanged()
    {
        OnChanged?.Invoke();
    }
}
