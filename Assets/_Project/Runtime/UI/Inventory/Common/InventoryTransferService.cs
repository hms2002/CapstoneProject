using UnityEngine;

// 책임: 인벤토리 간 아이템 이동에 필요한 source/target 컨테이너와 슬롯 정보를 전달한다.
public readonly struct InventoryTransferRequest
{
    public IItemContainer Source { get; }
    public int SourceIndex { get; }
    public IItemContainer Target { get; }
    public int TargetIndex { get; }
    public int SourceRelicLevel { get; }

    public InventoryTransferRequest(
        IItemContainer source,
        int sourceIndex,
        IItemContainer target,
        int targetIndex,
        int sourceRelicLevel)
    {
        Source = source;
        SourceIndex = sourceIndex;
        Target = target;
        TargetIndex = targetIndex;
        SourceRelicLevel = sourceRelicLevel;
    }
}

public enum InventoryTransferFailureReason
{
    None = 0,
    NoActiveDrag,
    MissingSource,
    MissingTarget,
    MissingSourceItem,
    TargetSlotUnavailable,
    SwapFailed,
    TargetRejectedItem,
    SourceRejectedItem,
    TargetSetFailed,
    SourceClearFailed,
    SourceSetFailed,
    LastWeaponProtected
}

// 책임: 인벤토리 이동 성공 여부와 실패 사유, 경고 팝업 코드를 반환한다.
public readonly struct InventoryTransferResult
{
    public bool Succeeded { get; }
    public InventoryTransferFailureReason FailureReason { get; }
    public WarningPopupCode WarningCode { get; }
    public bool HasWarning => WarningCode != WarningPopupCode.None;

    private InventoryTransferResult(
        bool succeeded,
        InventoryTransferFailureReason failureReason,
        WarningPopupCode warningCode)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
        WarningCode = warningCode;
    }

    public static InventoryTransferResult Success => new(true, InventoryTransferFailureReason.None, WarningPopupCode.None);

    public static InventoryTransferResult Failed(
        InventoryTransferFailureReason failureReason,
        WarningPopupCode warningCode = WarningPopupCode.None)
    {
        return new InventoryTransferResult(false, failureReason, warningCode);
    }
}

// 책임: 공용 아이템 컨테이너 간 이동/교환/유물 병합 규칙을 적용한다.
public static class InventoryTransferService
{
    public static InventoryTransferResult TryTransfer(InventoryTransferRequest request)
    {
        IItemContainer source = request.Source;
        IItemContainer target = request.Target;

        if (source == null)
            return InventoryTransferResult.Failed(InventoryTransferFailureReason.MissingSource);

        if (target == null)
            return InventoryTransferResult.Failed(InventoryTransferFailureReason.MissingTarget);

        if (!IsValidIndex(source, request.SourceIndex))
            return InventoryTransferResult.Failed(InventoryTransferFailureReason.MissingSourceItem);

        if (!IsValidIndex(target, request.TargetIndex))
            return InventoryTransferResult.Failed(InventoryTransferFailureReason.TargetSlotUnavailable);

        if (target == source)
        {
            return source.TrySwap(request.SourceIndex, request.TargetIndex)
                ? InventoryTransferResult.Success
                : InventoryTransferResult.Failed(InventoryTransferFailureReason.SwapFailed);
        }

        var srcItem = source.Get(request.SourceIndex);
        if (srcItem == null)
            return InventoryTransferResult.Failed(InventoryTransferFailureReason.MissingSourceItem);

        if (InventoryWeaponRetentionPolicy.WouldRemoveLastPlayerWeapon(
                source,
                request.SourceIndex,
                target,
                request.TargetIndex))
        {
            return InventoryTransferResult.Failed(
                InventoryTransferFailureReason.LastWeaponProtected,
                WarningPopupCode.LastWeaponCannotLeaveInventory);
        }

        int srcLvl = request.SourceRelicLevel;
        if (srcLvl <= 0 && srcItem is RelicDefinition && source is IRelicLevelProvider sourceLevelProvider)
            sourceLevelProvider.TryGetRelicLevel(request.SourceIndex, out srcLvl);

        if (TryMergeIntoExistingPlayerRelic(
                source,
                request.SourceIndex,
                target,
                srcItem,
                srcLvl,
                out InventoryTransferResult mergeResult))
        {
            return mergeResult;
        }

        int resolvedTargetIndex = ResolveRelicDropTargetIndex(target, request.TargetIndex, srcItem);
        var dstItem = target.Get(resolvedTargetIndex);

        int dstLvl = 0;
        if (dstItem is RelicDefinition && target is IRelicLevelProvider targetLevelProvider)
            targetLevelProvider.TryGetRelicLevel(resolvedTargetIndex, out dstLvl);

        if (!target.CanPlace(srcItem, resolvedTargetIndex, ignoreIndex: -1))
            return InventoryTransferResult.Failed(InventoryTransferFailureReason.TargetRejectedItem);

        if (!source.CanPlace(dstItem, request.SourceIndex, ignoreIndex: -1))
            return InventoryTransferResult.Failed(InventoryTransferFailureReason.SourceRejectedItem);

        bool targetSet;
        if (srcItem is RelicDefinition sourceRelic && target is IRelicSlotReceiver targetRelicReceiver && srcLvl > 0)
            targetSet = targetRelicReceiver.TrySetRelicWithLevel(resolvedTargetIndex, sourceRelic, srcLvl);
        else
            targetSet = target.TrySet(resolvedTargetIndex, srcItem);

        if (!targetSet)
            return InventoryTransferResult.Failed(InventoryTransferFailureReason.TargetSetFailed);

        if (srcItem is RelicDefinition && target is IRelicSlotReceiver)
        {
            var after = target.Get(resolvedTargetIndex);
            if (after != srcItem)
            {
                if (source.TrySet(request.SourceIndex, null))
                    return InventoryTransferResult.Success;

                target.TrySet(resolvedTargetIndex, dstItem);
                return InventoryTransferResult.Failed(InventoryTransferFailureReason.SourceClearFailed);
            }
        }

        bool sourceSet;
        if (dstItem is RelicDefinition targetRelic && source is IRelicSlotReceiver sourceRelicReceiver && dstLvl > 0)
            sourceSet = sourceRelicReceiver.TrySetRelicWithLevel(request.SourceIndex, targetRelic, dstLvl);
        else
            sourceSet = source.TrySet(request.SourceIndex, dstItem);

        if (!sourceSet)
        {
            if (dstItem is RelicDefinition rollbackRelic && target is IRelicSlotReceiver rollbackRelicReceiver && dstLvl > 0)
                rollbackRelicReceiver.TrySetRelicWithLevel(resolvedTargetIndex, rollbackRelic, dstLvl);
            else
                target.TrySet(resolvedTargetIndex, dstItem);

            return InventoryTransferResult.Failed(InventoryTransferFailureReason.SourceSetFailed);
        }

        return InventoryTransferResult.Success;
    }

    private static bool TryMergeIntoExistingPlayerRelic(
        IItemContainer source,
        int sourceIndex,
        IItemContainer target,
        ScriptableObject sourceItem,
        int sourceRelicLevel,
        out InventoryTransferResult result)
    {
        result = InventoryTransferResult.Failed(InventoryTransferFailureReason.TargetSetFailed);

        if (sourceItem is not RelicDefinition sourceRelic)
            return false;

        if (target is not PlayerRelicContainerAdapter playerRelicTarget)
            return false;

        if (!playerRelicTarget.HasExistingRelic(sourceRelic))
            return false;

        if (!playerRelicTarget.TryMergeExistingRelicWithLevel(sourceRelic, sourceRelicLevel))
            return true;

        result = source.TrySet(sourceIndex, null)
            ? InventoryTransferResult.Success
            : InventoryTransferResult.Failed(InventoryTransferFailureReason.SourceClearFailed);
        return true;
    }

    private static bool IsValidIndex(IItemContainer container, int index)
    {
        return container != null && index >= 0 && index < container.SlotCount;
    }

    private static int ResolveRelicDropTargetIndex(IItemContainer target, int requestedIndex, ScriptableObject srcItem)
    {
        if (target == null)
            return requestedIndex;

        var movingRelic = srcItem as RelicDefinition;
        if (movingRelic == null)
            return requestedIndex;

        var dstRelic = target.Get(requestedIndex) as RelicDefinition;
        if (dstRelic == null)
            return requestedIndex;

        if (dstRelic.relicId != movingRelic.relicId)
            return requestedIndex;

        for (int i = 0; i < target.SlotCount; i++)
        {
            if (i == requestedIndex)
                continue;

            if (!target.CanPlace(srcItem, i, ignoreIndex: -1))
                continue;

            return i;
        }

        return requestedIndex;
    }
}
