using System;
using System.Collections.Generic;

public sealed class MerchantRunStateService
{
    public MerchantRuntimeState GetOrCreateState(
        string merchantId,
        int slotCount,
        Func<List<MerchantStockEntryState>> stockFactory)
    {
        List<MerchantStockEntryState> generatedStock = CreateEntries(slotCount, stockFactory);

        if (string.IsNullOrWhiteSpace(merchantId))
            return new MerchantRuntimeState(string.Empty, generatedStock);

        GamePlayData data = GamePlayDataManager.Instance != null ? GamePlayDataManager.Instance.Data : null;
        if (data == null)
            return new MerchantRuntimeState(merchantId, generatedStock);

        data.merchantStates ??= new List<MerchantRuntimeState>();

        MerchantRuntimeState state = data.merchantStates.Find(x => x != null && x.merchantId == merchantId);
        if (state == null)
        {
            state = new MerchantRuntimeState(merchantId, generatedStock);
            data.merchantStates.Add(state);
            return state;
        }

        NormalizeStateSlots(state, slotCount, generatedStock);
        return state;
    }

    public void MarkSlotSold(MerchantRuntimeState runtimeState, int slotIndex)
    {
        if (runtimeState?.slots == null || slotIndex < 0 || slotIndex >= runtimeState.slots.Count)
            return;

        runtimeState.slots[slotIndex] ??= MerchantStockEntryState.Empty();
        runtimeState.slots[slotIndex].isSold = true;
    }

    private static void NormalizeStateSlots(
        MerchantRuntimeState state,
        int slotCount,
        List<MerchantStockEntryState> fallbackEntries)
    {
        state.slots ??= new List<MerchantStockEntryState>();

        if (state.slots.Count == slotCount)
        {
            for (int i = 0; i < state.slots.Count; i++)
                state.slots[i] ??= MerchantStockEntryState.Empty();
            return;
        }

        state.slots = fallbackEntries ?? new List<MerchantStockEntryState>();
    }

    private static List<MerchantStockEntryState> CreateEntries(
        int slotCount,
        Func<List<MerchantStockEntryState>> stockFactory)
    {
        List<MerchantStockEntryState> entries = stockFactory != null
            ? stockFactory.Invoke()
            : new List<MerchantStockEntryState>();

        entries ??= new List<MerchantStockEntryState>();

        if (entries.Count > slotCount)
            entries.RemoveRange(slotCount, entries.Count - slotCount);

        while (entries.Count < slotCount)
            entries.Add(MerchantStockEntryState.Empty());

        for (int i = 0; i < entries.Count; i++)
            entries[i] ??= MerchantStockEntryState.Empty();

        return entries;
    }
}
