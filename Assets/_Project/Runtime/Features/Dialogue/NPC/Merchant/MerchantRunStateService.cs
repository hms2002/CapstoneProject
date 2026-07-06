using System;
using System.Collections.Generic;

// 책임: 런 세션에 저장되는 상인 재고 상태를 생성, 조회, 갱신한다.
public sealed class MerchantRunStateService
{
    public delegate List<MerchantStockEntryState> StockFactory(
        int slotCount,
        IReadOnlyCollection<MerchantStockEntryState> excludedEntries);

    public MerchantRuntimeState GetOrCreateState(
        string merchantId,
        int slotCount,
        StockFactory stockFactory)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
            return new MerchantRuntimeState(string.Empty, CreateEntries(slotCount, stockFactory, null));

        GamePlayData data = RunSessionStore.Data;
        if (data == null)
            return new MerchantRuntimeState(merchantId, CreateEntries(slotCount, stockFactory, null));

        data.merchantStates ??= new List<MerchantRuntimeState>();

        MerchantRuntimeState state = data.merchantStates.Find(x => x != null && x.merchantId == merchantId);
        if (state == null)
        {
            state = new MerchantRuntimeState(merchantId, CreateEntries(slotCount, stockFactory, null));
            data.merchantStates.Add(state);
            return state;
        }

        EnsureMinimumStateSlots(state, slotCount, stockFactory);
        return state;
    }

    public void MarkSlotSold(MerchantRuntimeState runtimeState, int slotIndex)
    {
        if (runtimeState?.slots == null || slotIndex < 0 || slotIndex >= runtimeState.slots.Count)
            return;

        runtimeState.slots[slotIndex] ??= MerchantStockEntryState.Empty();
        runtimeState.slots[slotIndex].isSold = true;
    }

    public bool TryRefreshState(
        MerchantRuntimeState runtimeState,
        int maxRefreshCount,
        int slotCount,
        StockFactory stockFactory)
    {
        if (runtimeState == null || runtimeState.refreshCountUsed >= Math.Max(0, maxRefreshCount))
            return false;

        runtimeState.slots = CreateEntries(slotCount, stockFactory, null);
        runtimeState.refreshCountUsed++;
        return true;
    }

    private static void EnsureMinimumStateSlots(
        MerchantRuntimeState state,
        int slotCount,
        StockFactory stockFactory)
    {
        state.slots ??= new List<MerchantStockEntryState>();

        for (int i = 0; i < state.slots.Count; i++)
            state.slots[i] ??= MerchantStockEntryState.Empty();

        if (state.slots.Count >= slotCount)
            return;

        int missingCount = slotCount - state.slots.Count;
        List<MerchantStockEntryState> newEntries = CreateEntries(missingCount, stockFactory, state.slots);
        state.slots.AddRange(newEntries);
    }

    private static List<MerchantStockEntryState> CreateEntries(
        int slotCount,
        StockFactory stockFactory,
        IReadOnlyCollection<MerchantStockEntryState> excludedEntries)
    {
        slotCount = Math.Max(0, slotCount);
        List<MerchantStockEntryState> entries = stockFactory != null
            ? stockFactory.Invoke(slotCount, excludedEntries)
            : new List<MerchantStockEntryState>();

        entries ??= new List<MerchantStockEntryState>();

        if (entries.Count > slotCount)
            entries.RemoveRange(slotCount, entries.Count - slotCount);

        while (entries.Count < slotCount)
            entries.Add(MerchantStockEntryState.Empty());

        for (int i = 0; i < entries.Count; i++)
        {
            entries[i] ??= MerchantStockEntryState.Empty();
        }

        return entries;
    }
}
