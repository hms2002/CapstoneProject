using System;
using UnityEngine;

// 책임: 플레이어 소모품 인벤토리를 공용 IItemContainer 계약으로 노출한다.
public sealed class PlayerConsumableContainerAdapter : IItemContainer, IDisposable
{
    private readonly PlayerConsumableInventory inventory;
    public event Action OnChanged;

    public PlayerConsumableContainerAdapter(PlayerConsumableInventory inventory)
    {
        this.inventory = inventory;
        if (this.inventory != null)
            this.inventory.OnChanged += HandleChanged;
    }

    public int SlotCount => inventory != null ? inventory.SlotCount : 0;

    public ScriptableObject Get(int index)
    {
        return inventory != null ? inventory.GetConsumableInSlot(index) : null;
    }

    public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
    {
        if (inventory == null)
            return false;
        if (item == null)
            return true;

        return item is ConsumableDefinition consumable
            && inventory.CanPlaceConsumableInSlot(index, consumable);
    }

    public bool TrySet(int index, ScriptableObject item)
    {
        if (inventory == null)
            return false;
        if (item == null)
            return inventory.TrySetConsumableSlot(index, null);

        return item is ConsumableDefinition consumable
            && inventory.TrySetConsumableSlot(index, consumable);
    }

    public bool TrySwap(int a, int b)
    {
        return inventory != null && inventory.TrySwapConsumableSlots(a, b);
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

// 책임: 플레이어 무기 인벤토리를 공용 IItemContainer 계약으로 노출한다.
public sealed class PlayerWeaponContainerAdapter : IItemContainer, IDisposable
{
    private readonly WeaponInventory2D inventory;
    public event Action OnChanged;

    public PlayerWeaponContainerAdapter(WeaponInventory2D inventory)
    {
        this.inventory = inventory;
        if (this.inventory != null)
            this.inventory.OnInventoryChanged += HandleChanged;
    }

    public int SlotCount => inventory != null ? inventory.SlotCount : 0;

    public ScriptableObject Get(int index)
    {
        return inventory != null ? inventory.GetWeaponInSlot(index) : null;
    }

    public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
    {
        if (inventory == null)
            return false;
        if (item == null)
            return true;

        return item is WeaponDefinition weapon
            && inventory.CanPlaceWeaponInSlot(index, weapon);
    }

    public bool TrySet(int index, ScriptableObject item)
    {
        if (inventory == null)
            return false;
        if (item == null)
            return inventory.TrySetWeaponSlot(index, null);

        return item is WeaponDefinition weapon
            && inventory.TrySetWeaponSlot(index, weapon);
    }

    public bool TrySwap(int a, int b)
    {
        return inventory != null && inventory.TrySwapWeaponSlots(a, b);
    }

    public void Dispose()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= HandleChanged;
    }

    private void HandleChanged()
    {
        OnChanged?.Invoke();
    }
}

// 책임: 유물 인벤토리를 공통 아이템 컨테이너/레벨/수신 인터페이스로 노출한다.
public sealed class PlayerRelicContainerAdapter : IItemContainer, IDisposable, IRelicLevelProvider, IRelicSlotReceiver
{
    private readonly RelicInventory inventory;
    public event Action OnChanged;

    public PlayerRelicContainerAdapter(RelicInventory inventory)
    {
        this.inventory = inventory;
        if (this.inventory != null)
            this.inventory.OnChanged += HandleChanged;
    }

    public int SlotCount => inventory != null ? inventory.Capacity : 0;

    public ScriptableObject Get(int index)
    {
        return inventory != null ? inventory.GetRelicInSlot(index) : null;
    }

    public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
    {
        if (inventory == null)
            return false;
        if (item == null)
            return true;

        return item is RelicDefinition relic
            && inventory.CanPlaceRelicInSlot(index, relic, ignoreIndex);
    }

    public bool TrySet(int index, ScriptableObject item)
    {
        if (inventory == null)
            return false;
        if (item == null)
        {
            bool cleared = inventory.TrySetRelicSlot(index, null);
            if (!cleared)
                ShowRelicWarning(inventory.LastFailureResult);
            return cleared;
        }
        if (!(item is RelicDefinition relic))
            return false;

        bool ok = inventory.TrySetRelicSlot(index, relic);
        if (!ok)
            ShowRelicWarning(ResolveRelicFailure(relic, relic.dropLevel > 0 ? relic.dropLevel : 1));
        return ok;
    }

    public bool TrySwap(int a, int b)
    {
        return inventory != null && inventory.TrySwapRelicSlots(a, b);
    }

    public bool TryGetRelicLevel(int index, out int level)
    {
        level = inventory != null ? inventory.GetRelicLevelInSlot(index) : 0;
        return level > 0;
    }

    public bool TrySetRelicWithLevel(int index, RelicDefinition relic, int level)
    {
        if (inventory == null)
            return false;
        if (relic == null)
        {
            bool cleared = inventory.TrySetRelicSlot(index, null);
            if (!cleared)
                ShowRelicWarning(inventory.LastFailureResult);
            return cleared;
        }

        bool ok = inventory.TrySetRelicSlotWithLevel(index, relic, level);
        if (!ok)
            ShowRelicWarning(ResolveRelicFailure(relic, level));
        return ok;
    }

    public bool HasExistingRelic(RelicDefinition relic)
    {
        return inventory != null
            && relic != null
            && inventory.TryGetRelicLevelById(relic.relicId, out _);
    }

    public bool TryMergeExistingRelicWithLevel(RelicDefinition relic, int level)
    {
        if (!HasExistingRelic(relic))
            return false;

        int incomingLevel = ResolveIncomingRelicLevel(relic, level);
        RelicInventory.AcquireResult preview = inventory.PreviewAcquireOrUpgrade(relic, incomingLevel);
        if (preview != RelicInventory.AcquireResult.Success)
        {
            ShowRelicWarning(preview);
            return false;
        }

        RelicInventory.AcquireResult result = inventory.TryAcquireOrUpgradeDetailed(relic, incomingLevel);
        if (result != RelicInventory.AcquireResult.Success)
        {
            ShowRelicWarning(result);
            return false;
        }

        return true;
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

    private static void ShowRelicWarning(RelicInventory.AcquireResult result)
    {
        WarningPopupCode code = InventoryDeliveryWarningResolver.FromRelicAcquireResult(result);

        if (code != WarningPopupCode.None)
            WarningPopupPlayback.Show(code);
    }

    private RelicInventory.AcquireResult ResolveRelicFailure(RelicDefinition relic, int incomingLevel)
    {
        if (inventory != null && inventory.LastFailureResult == RelicInventory.AcquireResult.HealthTooLowForRelicChange)
            return inventory.LastFailureResult;

        if (inventory == null || relic == null)
            return RelicInventory.AcquireResult.InvalidDefinition;

        if (!inventory.TryGetRelicLevelById(relic.relicId, out int currentLevel))
            return RelicInventory.AcquireResult.InvalidDefinition;

        int gain = Mathf.Max(1, incomingLevel);
        int nextLevel = relic.ClampLevel(currentLevel + gain);
        return nextLevel == currentLevel
            ? RelicInventory.AcquireResult.AlreadyMaxLevel
            : RelicInventory.AcquireResult.InvalidDefinition;
    }

    private static int ResolveIncomingRelicLevel(RelicDefinition relic, int level)
    {
        if (level > 0)
            return Mathf.Max(1, level);

        return relic != null && relic.dropLevel > 0
            ? relic.dropLevel
            : 1;
    }
}
