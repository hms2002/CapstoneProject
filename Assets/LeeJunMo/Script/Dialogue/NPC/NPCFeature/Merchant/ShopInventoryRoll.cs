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
        MerchantPriceSettings priceSettings,
        IReadOnlyCollection<MerchantStockEntryState> excludedEntries = null)
    {
        var entries = new List<MerchantStockEntryState>(Mathf.Max(0, slotCount));
        if (slotCount <= 0 || ItemManager.Instance == null)
            return entries;

        List<WeaponDefinition> weaponPool = BuildWeaponPool();
        List<RelicDefinition> relicPool = BuildRelicPool();
        List<ConsumableDefinition> consumablePool = BuildConsumablePool();
        HashSet<string> excludedKeys = BuildExcludedKeys(excludedEntries);

        RemoveExcludedDefinitions(weaponPool, excludedKeys);
        RemoveExcludedDefinitions(relicPool, excludedKeys);
        RemoveExcludedDefinitions(consumablePool, excludedKeys);

        for (int i = 0; i < slotCount; i++)
        {
            List<WeightedKind> availableKinds = BuildAvailableKinds(
                weaponPool,
                relicPool,
                consumablePool,
                rollWeights);

            if (availableKinds.Count == 0)
                break;

            ShopPoolKind pickedKind = PickKind(availableKinds);
            ScriptableObject pickedDefinition = DrawDefinition(
                pickedKind,
                weaponPool,
                relicPool,
                consumablePool);

            if (pickedDefinition == null)
                continue;

            IInventoryItemDefinition commonDefinition = pickedDefinition.AsDef();
            if (commonDefinition == null || string.IsNullOrWhiteSpace(commonDefinition.ItemId))
                continue;

            entries.Add(new MerchantStockEntryState(
                commonDefinition.Kind,
                commonDefinition.ItemId,
                priceSettings.ResolvePrice(pickedDefinition)));
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

    private static string BuildItemKey(InventoryItemKind kind, string itemId)
    {
        return $"{kind}:{itemId}";
    }

    private static List<WeightedKind> BuildAvailableKinds(
        List<WeaponDefinition> weaponPool,
        List<RelicDefinition> relicPool,
        List<ConsumableDefinition> consumablePool,
        ShopStockRollWeights rollWeights)
    {
        var availableKinds = new List<WeightedKind>(3);

        if (weaponPool != null && weaponPool.Count > 0 && rollWeights.weaponWeight > 0)
            availableKinds.Add(new WeightedKind(ShopPoolKind.Weapon, rollWeights.weaponWeight));

        if (relicPool != null && relicPool.Count > 0 && rollWeights.relicWeight > 0)
            availableKinds.Add(new WeightedKind(ShopPoolKind.Relic, rollWeights.relicWeight));

        if (consumablePool != null && consumablePool.Count > 0 && rollWeights.consumableWeight > 0)
            availableKinds.Add(new WeightedKind(ShopPoolKind.Consumable, rollWeights.consumableWeight));

        return availableKinds;
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
