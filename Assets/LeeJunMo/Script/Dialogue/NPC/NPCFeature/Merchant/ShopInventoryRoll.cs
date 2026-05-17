using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ShopStockRollWeights
{
    [Min(0)] public int weaponWeight;
    [Min(0)] public int relicWeight;
    [Min(0)] public int consumableWeight;
}

[Serializable]
public enum ShopSlotItemFilter
{
    Any = 0,
    Weapon = 1,
    Relic = 2,
    Consumable = 3
}

[Serializable]
public struct MerchantPriceSettings
{
    [Min(0)] public int weaponPrice;
    [Min(0)] public int commonRelicPrice;
    [Min(0)] public int rareRelicPrice;
    [Min(0)] public int epicRelicPrice;
    [Min(0)] public int consumablePrice;

    public int ResolvePrice(ScriptableObject definition)
    {
        return definition switch
        {
            WeaponDefinition => weaponPrice,
            RelicDefinition relic => ResolveRelicPrice(relic.rarity),
            ConsumableDefinition => consumablePrice,
            _ => 0
        };
    }

    public MerchantPriceSettings WithDiscount(float discountRate)
    {
        discountRate = Mathf.Clamp01(discountRate);
        return new MerchantPriceSettings
        {
            weaponPrice = ApplyDiscount(weaponPrice, discountRate),
            commonRelicPrice = ApplyDiscount(commonRelicPrice, discountRate),
            rareRelicPrice = ApplyDiscount(rareRelicPrice, discountRate),
            epicRelicPrice = ApplyDiscount(epicRelicPrice, discountRate),
            consumablePrice = ApplyDiscount(consumablePrice, discountRate)
        };
    }

    private int ResolveRelicPrice(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => commonRelicPrice,
            ItemRarity.Rare => rareRelicPrice,
            ItemRarity.Epic => epicRelicPrice,
            _ => commonRelicPrice
        };
    }

    private static int ApplyDiscount(int price, float discountRate)
    {
        return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, price) * (1f - discountRate)));
    }
}

public sealed class ShopInventoryRoll
{
    private enum ShopPoolKind
    {
        Weapon,
        Relic,
        Consumable
    }

    private readonly struct WeightedKind
    {
        public readonly ShopPoolKind kind;
        public readonly int weight;

        public WeightedKind(ShopPoolKind kind, int weight)
        {
            this.kind = kind;
            this.weight = weight;
        }
    }

    public List<MerchantStockEntryState> RollStock(
        int slotCount,
        ShopStockRollWeights rollWeights,
        int maxWeaponSlots,
        int maxConsumableSlots,
        MerchantPriceSettings priceSettings,
        IReadOnlyCollection<string> excludedWeaponIds = null,
        IReadOnlyCollection<MerchantStockEntryState> excludedEntries = null,
        IReadOnlyList<ShopSlotItemFilter> slotFilters = null)
    {
        var entries = new List<MerchantStockEntryState>(Mathf.Max(0, slotCount));
        if (slotCount <= 0 || ItemManager.Instance == null)
            return entries;

        maxWeaponSlots = Mathf.Max(0, maxWeaponSlots);
        maxConsumableSlots = Mathf.Max(0, maxConsumableSlots);

        List<WeaponDefinition> weaponPool = BuildWeaponPool();
        List<RelicDefinition> relicPool = BuildRelicPool();
        List<ConsumableDefinition> consumablePool = BuildConsumablePool();
        HashSet<string> excludedKeys = BuildExcludedKeys(excludedEntries);

        RemoveExcludedDefinitions(weaponPool, excludedKeys);
        RemoveExcludedDefinitions(relicPool, excludedKeys);
        RemoveExcludedDefinitions(consumablePool, excludedKeys);
        RemoveExcludedWeapons(weaponPool, excludedWeaponIds);

        int weaponSlotCount = 0;
        int consumableSlotCount = 0;

        for (int i = 0; i < slotCount; i++)
        {
            ShopSlotItemFilter slotFilter = ResolveSlotFilter(slotFilters, i);
            List<WeightedKind> availableKinds = BuildAvailableKinds(
                weaponPool,
                relicPool,
                consumablePool,
                rollWeights,
                weaponSlotCount,
                maxWeaponSlots,
                consumableSlotCount,
                maxConsumableSlots,
                slotFilter);

            if (availableKinds.Count == 0)
            {
                entries.Add(MerchantStockEntryState.Empty());
                continue;
            }

            ShopPoolKind pickedKind = PickKind(availableKinds);
            ScriptableObject pickedDefinition = DrawDefinition(
                pickedKind,
                weaponPool,
                relicPool,
                consumablePool);

            if (pickedDefinition == null)
            {
                entries.Add(MerchantStockEntryState.Empty());
                continue;
            }

            IInventoryItemDefinition commonDefinition = pickedDefinition.AsDef();
            if (commonDefinition == null || string.IsNullOrWhiteSpace(commonDefinition.ItemId))
            {
                entries.Add(MerchantStockEntryState.Empty());
                continue;
            }

            entries.Add(new MerchantStockEntryState(
                commonDefinition.Kind,
                commonDefinition.ItemId,
                priceSettings.ResolvePrice(pickedDefinition)));

            if (commonDefinition.Kind == InventoryItemKind.Weapon)
                weaponSlotCount++;
            else if (commonDefinition.Kind == InventoryItemKind.Consumable)
                consumableSlotCount++;
        }

        while (entries.Count < slotCount)
            entries.Add(MerchantStockEntryState.Empty());

        return entries;
    }

    private static List<WeaponDefinition> BuildWeaponPool()
    {
        var pool = new List<WeaponDefinition>();
        List<string> unlockedIds = ItemManager.Instance.GetUnlockedWeaponIDs();

        for (int i = 0; i < unlockedIds.Count; i++)
        {
            WeaponDefinition definition = ItemManager.Instance.GetWeaponData(unlockedIds[i]);
            if (definition != null)
                pool.Add(definition);
        }

        return pool;
    }

    private static List<RelicDefinition> BuildRelicPool()
    {
        var pool = new List<RelicDefinition>();
        List<string> unlockedIds = ItemManager.Instance.GetUnlockedRelicIDs();

        for (int i = 0; i < unlockedIds.Count; i++)
        {
            RelicDefinition definition = ItemManager.Instance.GetRelicData(unlockedIds[i]);
            if (definition != null)
                pool.Add(definition);
        }

        return pool;
    }

    private static List<ConsumableDefinition> BuildConsumablePool()
    {
        List<ConsumableDefinition> consumables = ItemManager.Instance.GetAllConsumables();
        return consumables ?? new List<ConsumableDefinition>();
    }

    private static HashSet<string> BuildExcludedKeys(IReadOnlyCollection<MerchantStockEntryState> excludedEntries)
    {
        if (excludedEntries == null || excludedEntries.Count == 0)
            return null;

        var excludedKeys = new HashSet<string>();
        foreach (MerchantStockEntryState entry in excludedEntries)
        {
            if (entry == null || !entry.HasItem)
                continue;

            excludedKeys.Add(BuildItemKey(entry.kind, entry.itemId));
        }

        return excludedKeys;
    }

    private static void RemoveExcludedDefinitions<T>(
        List<T> pool,
        HashSet<string> excludedKeys) where T : ScriptableObject
    {
        if (pool == null || excludedKeys == null || excludedKeys.Count == 0)
            return;

        for (int i = pool.Count - 1; i >= 0; i--)
        {
            IInventoryItemDefinition definition = pool[i] != null ? pool[i].AsDef() : null;
            if (definition != null && excludedKeys.Contains(BuildItemKey(definition.Kind, definition.ItemId)))
                pool.RemoveAt(i);
        }
    }

    private static void RemoveExcludedWeapons(
        List<WeaponDefinition> pool,
        IReadOnlyCollection<string> excludedWeaponIds)
    {
        if (pool == null || excludedWeaponIds == null || excludedWeaponIds.Count == 0)
            return;

        for (int i = pool.Count - 1; i >= 0; i--)
        {
            WeaponDefinition weapon = pool[i];
            if (weapon != null && IsExcludedWeaponId(excludedWeaponIds, weapon.weaponId))
                pool.RemoveAt(i);
        }
    }

    private static bool IsExcludedWeaponId(IReadOnlyCollection<string> excludedWeaponIds, string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            return false;

        foreach (string excludedWeaponId in excludedWeaponIds)
        {
            if (string.Equals(excludedWeaponId, weaponId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string BuildItemKey(InventoryItemKind kind, string itemId)
    {
        return $"{kind}:{itemId}";
    }

    private static List<WeightedKind> BuildAvailableKinds(
        List<WeaponDefinition> weaponPool,
        List<RelicDefinition> relicPool,
        List<ConsumableDefinition> consumablePool,
        ShopStockRollWeights rollWeights,
        int weaponSlotCount,
        int maxWeaponSlots,
        int consumableSlotCount,
        int maxConsumableSlots,
        ShopSlotItemFilter slotFilter)
    {
        var availableKinds = new List<WeightedKind>(3);

        if (SlotAllowsKind(slotFilter, ShopPoolKind.Weapon) &&
            weaponSlotCount < maxWeaponSlots &&
            weaponPool != null &&
            weaponPool.Count > 0 &&
            rollWeights.weaponWeight > 0)
        {
            availableKinds.Add(new WeightedKind(ShopPoolKind.Weapon, rollWeights.weaponWeight));
        }

        if (SlotAllowsKind(slotFilter, ShopPoolKind.Relic) &&
            relicPool != null &&
            relicPool.Count > 0 &&
            rollWeights.relicWeight > 0)
        {
            availableKinds.Add(new WeightedKind(ShopPoolKind.Relic, rollWeights.relicWeight));
        }

        if (SlotAllowsKind(slotFilter, ShopPoolKind.Consumable) &&
            consumableSlotCount < maxConsumableSlots &&
            consumablePool != null &&
            consumablePool.Count > 0 &&
            rollWeights.consumableWeight > 0)
        {
            availableKinds.Add(new WeightedKind(ShopPoolKind.Consumable, rollWeights.consumableWeight));
        }

        return availableKinds;
    }

    private static ShopSlotItemFilter ResolveSlotFilter(IReadOnlyList<ShopSlotItemFilter> slotFilters, int slotIndex)
    {
        if (slotFilters == null || slotIndex < 0 || slotIndex >= slotFilters.Count)
            return ShopSlotItemFilter.Any;

        return slotFilters[slotIndex];
    }

    private static bool SlotAllowsKind(ShopSlotItemFilter slotFilter, ShopPoolKind poolKind)
    {
        return slotFilter == ShopSlotItemFilter.Any ||
               (slotFilter == ShopSlotItemFilter.Weapon && poolKind == ShopPoolKind.Weapon) ||
               (slotFilter == ShopSlotItemFilter.Relic && poolKind == ShopPoolKind.Relic) ||
               (slotFilter == ShopSlotItemFilter.Consumable && poolKind == ShopPoolKind.Consumable);
    }

    private static ShopPoolKind PickKind(List<WeightedKind> availableKinds)
    {
        int totalWeight = 0;
        for (int i = 0; i < availableKinds.Count; i++)
            totalWeight += Mathf.Max(0, availableKinds[i].weight);

        if (totalWeight <= 0)
            return availableKinds[0].kind;

        int randomValue = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        for (int i = 0; i < availableKinds.Count; i++)
        {
            cumulative += Mathf.Max(0, availableKinds[i].weight);
            if (randomValue < cumulative)
                return availableKinds[i].kind;
        }

        return availableKinds[availableKinds.Count - 1].kind;
    }

    private static ScriptableObject DrawDefinition(
        ShopPoolKind poolKind,
        List<WeaponDefinition> weaponPool,
        List<RelicDefinition> relicPool,
        List<ConsumableDefinition> consumablePool)
    {
        return poolKind switch
        {
            ShopPoolKind.Weapon => DrawFromPool(weaponPool),
            ShopPoolKind.Relic => DrawFromPool(relicPool),
            ShopPoolKind.Consumable => DrawFromPool(consumablePool),
            _ => null
        };
    }

    private static T DrawFromPool<T>(List<T> pool) where T : ScriptableObject
    {
        if (pool == null || pool.Count == 0)
            return null;

        int pickedIndex = UnityEngine.Random.Range(0, pool.Count);
        T picked = pool[pickedIndex];
        pool.RemoveAt(pickedIndex);
        return picked;
    }
}
