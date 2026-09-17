using System;
using UnityEngine;

/// <summary>
/// 책임 : 상자 인벤토리를 공용 아이템 컨테이너/유물 레벨/유물 수신 계약으로 노출한다.
/// </summary>
public sealed class ChestContainerAdapter : IItemContainer, IDisposable, IRelicLevelProvider, IRelicSlotReceiver
{
    private readonly ChestInventory inventory;
    public event Action OnChanged;

    public ChestContainerAdapter(ChestInventory inventory, bool selectionOnly = false)
    {
        this.inventory = inventory;
        IsSelectionOnly = selectionOnly;
        if (this.inventory != null)
            this.inventory.OnChanged += HandleChanged;
    }

    public ChestInventory Inventory => inventory;
    public bool IsSelectionOnly { get; }

    public int SlotCount => inventory != null ? inventory.Capacity : 0;

    public ScriptableObject Get(int index)
    {
        return inventory != null ? inventory.Get(index) : null;
    }

    public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
    {
        return !IsSelectionOnly && inventory != null && index >= 0 && index < inventory.Capacity &&
            (item == null || inventory.CanReturnAcquisition(item));
    }

    public bool TrySet(int index, ScriptableObject item)
    {
        return !IsSelectionOnly && inventory != null && inventory.Set(index, item);
    }

    public bool TrySwap(int a, int b)
    {
        return !IsSelectionOnly && inventory != null && inventory.Swap(a, b);
    }

    public bool TryGetRelicLevel(int index, out int level)
    {
        level = inventory != null ? inventory.GetRelicLevelInSlot(index) : 0;
        return level > 0;
    }

    public bool TrySetRelicWithLevel(int index, RelicDefinition relic, int level)
    {
        return !IsSelectionOnly && inventory != null && inventory.SetRelicWithLevel(index, relic, level);
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
