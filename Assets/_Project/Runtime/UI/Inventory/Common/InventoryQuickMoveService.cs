using System;
using System.Collections.Generic;
using CapstoneAudio;
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
        if (ItemContainerGroupRegistry.IsInspectionOnly)
            return InventoryQuickMoveResult.Ignored;

        if (source == null)
            return InventoryQuickMoveResult.Ignored;

        ScriptableObject item = source.Get(sourceIndex);
        if (item == null)
            return InventoryQuickMoveResult.Ignored;

        IInventoryItemDefinition definition = item.AsDef();
        if (definition == null)
            return InventoryQuickMoveResult.Ignored;

        IItemContainer chest = ItemContainerGroupRegistry.Chest;
        if (chest is ChestContainerAdapter { IsSelectionOnly: true } && source is not WorldLootContainerAdapter)
            return InventoryQuickMoveResult.Ignored;
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
    private static readonly SoundRef FailMigrateItemSound = SoundRef.FromKey("sound_ui_FailMigrateItem");

    public static void ExecuteDrop(IItemContainer target, int targetIndex, Action refresh)
    {
        if (ItemContainerGroupRegistry.IsInspectionOnly)
        {
            ItemDragContext.CancelActiveDragSession();
            refresh?.Invoke();
            return;
        }

        if (target == null || !ItemDragContext.Active)
            return;

        InventoryTransferResult result = ItemDragContext.TryDropWithResult(target, targetIndex);
        if (!result.Succeeded && result.FailureReason != InventoryTransferFailureReason.None)
            SoundPlaybackUtility.Play(FailMigrateItemSound);
        if (result.HasWarning)
            UIManager.Instance?.ShowWarning(result.WarningCode);
        refresh?.Invoke();
    }

    public static void ExecuteQuickMove(IItemContainer source, int sourceIndex, Action refresh)
    {
        if (ItemContainerGroupRegistry.IsInspectionOnly)
        {
            refresh?.Invoke();
            return;
        }

        if (source == null)
            return;

        InventoryQuickMoveResult result = InventoryQuickMoveService.TryMove(source, sourceIndex);
        if (!result.Succeeded && result.FailureReason != InventoryTransferFailureReason.None)
            SoundPlaybackUtility.Play(FailMigrateItemSound);
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

/// <summary>
/// Reserves destinations for the entire chest selection before any inventory is changed.
/// Uses the normal transfer path after capacity and relic-level validation.
/// </summary>
public static class ChestSelectionTransferService
{
    public const string InventoryFullMessage = "인벤토리 공간이 부족합니다. 인벤토리 아이템을 버리는 구역으로 드래그해 버린 후 다시 확정해 주세요.";

    public static InventoryTransferResult TryCommitPlan(IReadOnlyList<InventoryTransferRequest> plan)
        => TryCommitPlanWithFailure(plan, out _);

    public static InventoryTransferResult TryCommitPlanWithFailure(IReadOnlyList<InventoryTransferRequest> plan, out int failedSourceIndex)
    {
        failedSourceIndex = -1;
        var completed = new List<(InventoryTransferRequest request, ScriptableObject item,
            ScriptableObject previous, int previousLevel)>();
        foreach (InventoryTransferRequest request in plan)
        {
            ScriptableObject item = request.Source.Get(request.SourceIndex);
            ScriptableObject previous = request.Target.Get(request.TargetIndex);
            int previousLevel = 0;
            if (request.Target is IRelicLevelProvider provider)
                provider.TryGetRelicLevel(request.TargetIndex, out previousLevel);
            InventoryTransferResult result = InventoryTransferService.TryTransfer(request);
            if (result.Succeeded)
            {
                completed.Add((request, item, previous, previousLevel));
                continue;
            }

            failedSourceIndex = request.SourceIndex;
            // No frame or player input occurs between writes. Undo completed transfers
            // if a gameplay rule (for example linked health compensation) rejects a later one.
            for (int i = completed.Count - 1; i >= 0; i--)
            {
                var entry = completed[i];
                bool restored = entry.previous is RelicDefinition relic && entry.request.Target is IRelicSlotReceiver receiver
                    ? receiver.TrySetRelicWithLevel(entry.request.TargetIndex, relic, entry.previousLevel)
                    : entry.request.Target.TrySet(entry.request.TargetIndex, entry.previous);
                if (!restored)
                {
                    Debug.LogError("[ChestSelection] Inventory rejected rollback; acquired item remains in the player inventory.");
                    continue;
                }
                var chest = (ChestContainerAdapter)entry.request.Source;
                if (entry.item is RelicDefinition sourceRelic)
                    chest.TrySetRelicWithLevel(entry.request.SourceIndex, sourceRelic, entry.request.SourceRelicLevel);
                else chest.TrySet(entry.request.SourceIndex, entry.item);
                chest.Inventory.RecordReturn(entry.item);
            }
            return result;
        }
        return InventoryTransferResult.Success;
    }

    public static bool TryCreatePlan(ChestContainerAdapter source, IReadOnlyList<int> selection,
        IItemContainer consumables, IItemContainer weapons, IItemContainer relics,
        out List<InventoryTransferRequest> plan, out string warning)
        => TryCreatePlanWithFailure(source, selection, consumables, weapons, relics, out plan, out warning, out _);

    public static bool TryCreatePlanWithFailure(ChestContainerAdapter source, IReadOnlyList<int> selection,
        IItemContainer consumables, IItemContainer weapons, IItemContainer relics,
        out List<InventoryTransferRequest> plan, out string warning, out int failedSourceIndex)
    {
        failedSourceIndex = -1;
        plan = new List<InventoryTransferRequest>();
        warning = InventoryFullMessage;
        if (source?.Inventory == null || source.IsSelectionOnly || selection == null || selection.Count == 0 ||
            selection.Count + source.Inventory.AcquiredCount > ChestInventory.AcquisitionLimit)
            return false;

        var reservations = new Dictionary<IItemContainer, ScriptableObject[]>();
        var levels = new Dictionary<IItemContainer, int[]>();
        var sourceIndices = new HashSet<int>();
        foreach (int sourceIndex in selection)
        {
            failedSourceIndex = sourceIndex;
            ScriptableObject item = source.Get(sourceIndex);
            if (item == null || item.AsDef() == null || !sourceIndices.Add(sourceIndex)) return false;
            if (item is ParcelRelicDefinition)
            {
                warning = "이 아이템은 상자에서 이동할 수 없습니다.";
                return false;
            }
            IItemContainer target = item.AsDef().Kind switch
            {
                InventoryItemKind.Consumable => consumables,
                InventoryItemKind.Weapon => weapons,
                _ => relics
            };
            if (target == null) return false;
            if (!reservations.TryGetValue(target, out ScriptableObject[] items))
            {
                items = new ScriptableObject[target.SlotCount];
                var storedLevels = new int[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    items[i] = target.Get(i);
                    if (target is IRelicLevelProvider provider) provider.TryGetRelicLevel(i, out storedLevels[i]);
                }
                reservations.Add(target, items);
                levels.Add(target, storedLevels);
            }

            int destination = -1;
            var relic = item as RelicDefinition;
            // The normal player relic adapter merges equal ids, even when all slots are occupied.
            if (relic != null && target is PlayerRelicContainerAdapter)
                for (int i = 0; i < items.Length; i++)
                    if (items[i] is RelicDefinition existing && existing.relicId == relic.relicId)
                    { destination = i; break; }
            if (destination < 0)
                for (int i = 0; i < items.Length; i++)
                    if (items[i] == null && target.CanPlace(item, i))
                    { destination = i; break; }
            if (destination < 0) return false;

            int incomingLevel = source.Inventory.GetRelicLevelInSlot(sourceIndex);
            if (relic != null)
            {
                int previousLevel = levels[target][destination];
                int resultingLevel = relic.ClampLevel(previousLevel + Mathf.Max(1, incomingLevel));
                if (resultingLevel <= previousLevel)
                {
                    warning = "선택한 유물은 이미 최대 레벨입니다. 다른 아이템을 선택해 주세요.";
                    return false;
                }
                if (target is PlayerRelicContainerAdapter playerRelics &&
                    playerRelics.PreviewSelection(destination, relic, resultingLevel) != RelicInventory.AcquireResult.Success)
                {
                    warning = "현재 상태에서는 선택한 유물을 획득할 수 없습니다. 체력과 유물 상태를 확인해 주세요.";
                    return false;
                }
                levels[target][destination] = resultingLevel;
            }
            items[destination] = item;
            plan.Add(new InventoryTransferRequest(source, sourceIndex, target, destination, incomingLevel));
        }
        failedSourceIndex = -1;
        warning = null;
        return true;
    }
}
