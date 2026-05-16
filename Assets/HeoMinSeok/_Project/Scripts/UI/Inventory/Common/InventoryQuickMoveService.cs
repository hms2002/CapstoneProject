using System;
using UnityEngine;

public readonly struct InventoryQuickMoveResult
{
    public bool Succeeded { get; }
    public WarningPopupCode WarningCode { get; }
    public InventoryTransferFailureReason FailureReason { get; }
    public bool HasWarning => WarningCode != WarningPopupCode.None;

    private InventoryQuickMoveResult(
        bool succeeded,
        WarningPopupCode warningCode,
        InventoryTransferFailureReason failureReason)
    {
        Succeeded = succeeded;
        WarningCode = warningCode;
        FailureReason = failureReason;
    }

    public static InventoryQuickMoveResult Ignored => new(false, WarningPopupCode.None, InventoryTransferFailureReason.None);
    public static InventoryQuickMoveResult Moved => new(true, WarningPopupCode.None, InventoryTransferFailureReason.None);
    public static InventoryQuickMoveResult Failed => new(false, WarningPopupCode.None, InventoryTransferFailureReason.None);

    public static InventoryQuickMoveResult FromTransfer(InventoryTransferResult transferResult)
    {
        return new InventoryQuickMoveResult(
            transferResult.Succeeded,
            transferResult.WarningCode,
            transferResult.FailureReason);
    }

    public static InventoryQuickMoveResult Blocked(
        WarningPopupCode warningCode,
        InventoryTransferFailureReason failureReason = InventoryTransferFailureReason.TargetSlotUnavailable)
    {
        return new InventoryQuickMoveResult(false, warningCode, failureReason);
    }
}

/// <summary>
/// Resolves and executes right-click inventory quick moves for the currently active container group.
/// </summary>
public static class InventoryQuickMoveService
{
    public static InventoryQuickMoveResult TryMove(IItemContainer source, int sourceIndex)
    {
        if (source == null)
            return InventoryQuickMoveResult.Ignored;

        ScriptableObject item = source.Get(sourceIndex);
        if (item == null)
            return InventoryQuickMoveResult.Ignored;

        IInventoryItemDefinition definition = item.AsDef();
        if (definition == null)
            return InventoryQuickMoveResult.Ignored;

        IItemContainer chest = ItemContainerGroupRegistry.Chest;
        IItemContainer consumableEquip = ItemContainerGroupRegistry.ConsumableEquip;
        IItemContainer weaponEquip = ItemContainerGroupRegistry.WeaponEquip;
        IItemContainer relicEquip = ItemContainerGroupRegistry.RelicEquip;

        if (consumableEquip == null || weaponEquip == null || relicEquip == null)
            return InventoryQuickMoveResult.Ignored;

        if (!TryResolveTarget(
                source,
                item,
                definition,
                chest,
                consumableEquip,
                weaponEquip,
                relicEquip,
                out IItemContainer target,
                out int targetIndex))
        {
            return InventoryQuickMoveResult.Ignored;
        }

        if (target == null)
            return InventoryQuickMoveResult.Ignored;

        if (targetIndex < 0)
        {
            return InventoryQuickMoveResult.FromTransfer(InventoryTransferResult.Failed(
                InventoryTransferFailureReason.TargetSlotUnavailable,
                InventoryDeliveryWarningResolver.FromItem(item)));
        }

        int relicLevel = 0;
        if (item is RelicDefinition && source is IRelicLevelProvider levelProvider)
            levelProvider.TryGetRelicLevel(sourceIndex, out relicLevel);

        ItemDragContext.Begin(source, sourceIndex, item, relicLevel);
        InventoryTransferResult transferResult = ItemDragContext.TryDropWithResult(target, targetIndex);
        DragIcon.Instance?.Hide();
        ItemDragContext.Clear();

        return InventoryQuickMoveResult.FromTransfer(transferResult);
    }

    private static bool TryResolveTarget(
        IItemContainer source,
        ScriptableObject item,
        IInventoryItemDefinition definition,
        IItemContainer chest,
        IItemContainer consumableEquip,
        IItemContainer weaponEquip,
        IItemContainer relicEquip,
        out IItemContainer target,
        out int targetIndex)
    {
        target = null;
        targetIndex = -1;

        if (source == chest && chest != null)
            return TryResolveChestToPlayerTarget(item, definition, consumableEquip, weaponEquip, relicEquip, out target, out targetIndex);

        if (source is WorldLootContainerAdapter)
            return TryResolveWorldLootToPlayerTarget(item, definition, consumableEquip, weaponEquip, relicEquip, out target, out targetIndex);

        if (source == consumableEquip && chest != null)
            return ResolveFirstEmpty(chest, item, out target, out targetIndex);

        if (source == weaponEquip && chest != null)
            return ResolveFirstEmpty(chest, item, out target, out targetIndex);

        if (source == relicEquip && chest != null)
            return ResolveFirstEmpty(chest, item, out target, out targetIndex);

        return false;
    }

    private static bool TryResolveChestToPlayerTarget(
        ScriptableObject item,
        IInventoryItemDefinition definition,
        IItemContainer consumableEquip,
        IItemContainer weaponEquip,
        IItemContainer relicEquip,
        out IItemContainer target,
        out int targetIndex)
    {
        target = null;
        targetIndex = -1;

        if (definition.Kind == InventoryItemKind.Consumable)
            return ResolveFirstEmpty(consumableEquip, item, out target, out targetIndex);

        if (definition.Kind == InventoryItemKind.Weapon)
            return ResolveFirstEmpty(weaponEquip, item, out target, out targetIndex);

        target = relicEquip;
        targetIndex = item is RelicDefinition relic
            ? FindRelicQuickMoveIndex(target, relic)
            : FindFirstEmptyIndex(target, item);
        return true;
    }

    private static bool TryResolveWorldLootToPlayerTarget(
        ScriptableObject item,
        IInventoryItemDefinition definition,
        IItemContainer consumableEquip,
        IItemContainer weaponEquip,
        IItemContainer relicEquip,
        out IItemContainer target,
        out int targetIndex)
    {
        target = null;
        targetIndex = -1;

        if (definition.Kind == InventoryItemKind.Consumable)
            return ResolveFirstEmpty(consumableEquip, item, out target, out targetIndex);

        if (definition.Kind == InventoryItemKind.Weapon)
            return ResolveFirstEmpty(weaponEquip, item, out target, out targetIndex);

        if (item is RelicDefinition relic)
        {
            target = relicEquip;
            targetIndex = FindRelicQuickMoveIndex(target, relic);
            return true;
        }

        return false;
    }

    private static bool ResolveFirstEmpty(
        IItemContainer container,
        ScriptableObject item,
        out IItemContainer target,
        out int targetIndex)
    {
        target = container;
        targetIndex = FindFirstEmptyIndex(container, item);
        return true;
    }

    private static int FindFirstEmptyIndex(IItemContainer target, ScriptableObject moving)
    {
        if (target == null)
            return -1;

        for (int i = 0; i < target.SlotCount; i++)
        {
            if (target.Get(i) != null)
                continue;
            if (!target.CanPlace(moving, i))
                continue;
            return i;
        }

        return -1;
    }

    private static int FindSameRelicIndex(IItemContainer target, RelicDefinition relic)
    {
        if (target == null || relic == null)
            return -1;

        for (int i = 0; i < target.SlotCount; i++)
        {
            if (target.Get(i) is not RelicDefinition existing)
                continue;
            if (existing.relicId != relic.relicId)
                continue;
            return i;
        }

        return -1;
    }

    private static int FindAnyPlaceableIndex(IItemContainer target, ScriptableObject moving, int excludeIndex = -1)
    {
        if (target == null)
            return -1;

        for (int i = 0; i < target.SlotCount; i++)
        {
            if (i == excludeIndex)
                continue;
            if (!target.CanPlace(moving, i))
                continue;
            return i;
        }

        return -1;
    }

    private static int FindRelicQuickMoveIndex(IItemContainer target, RelicDefinition relic)
    {
        if (target == null || relic == null)
            return -1;

        int emptyIndex = FindFirstEmptyIndex(target, relic);
        if (emptyIndex >= 0)
            return emptyIndex;

        int sameRelicIndex = FindSameRelicIndex(target, relic);
        if (sameRelicIndex >= 0)
        {
            int mergeProxyIndex = FindAnyPlaceableIndex(target, relic, excludeIndex: sameRelicIndex);
            return mergeProxyIndex >= 0 ? mergeProxyIndex : sameRelicIndex;
        }

        return -1;
    }

}

/// <summary>
/// Handles slot-level transfer execution side effects so ItemSlotUI can stay focused on input and visuals.
/// </summary>
public static class InventorySlotTransferInteractionService
{
    public static void ExecuteDrop(IItemContainer target, int targetIndex, Action refresh)
    {
        if (target == null || !ItemDragContext.Active)
            return;

        ItemDragContext.TryDrop(target, targetIndex);
        refresh?.Invoke();
    }

    public static void ExecuteQuickMove(IItemContainer source, int sourceIndex, Action refresh)
    {
        if (source == null)
            return;

        InventoryQuickMoveResult result = InventoryQuickMoveService.TryMove(source, sourceIndex);
        ShowWarning(result);
        refresh?.Invoke();
    }

    private static void ShowWarning(InventoryQuickMoveResult result)
    {
        if (!result.HasWarning)
            return;

        UIManager.Instance?.ShowWarning(result.WarningCode);
    }
}
