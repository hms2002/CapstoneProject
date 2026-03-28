using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum MonsterLootType
{
    None,
    Weapon,
    Relic,
    Consumable,
    FieldItem,
}

public sealed class LootRollService
{
    public int PickCount(List<DropCountOption> options)
    {
        if (options == null || options.Count == 0)
            return 0;

        int total = options.Sum(o => o.weight);
        if (total <= 0)
            return 0;

        int rand = Random.Range(0, total);
        int sum = 0;
        foreach (var opt in options)
        {
            sum += opt.weight;
            if (rand < sum)
                return opt.count;
        }

        return options.Last().count;
    }

    public int PickCountInRange(List<DropCountOption> options, int minCount, int maxCount)
    {
        if (options == null || options.Count == 0)
            return Mathf.Max(0, minCount);

        int definedMin = options.Min(o => o.count);
        int definedMax = options.Max(o => o.count);

        int requestedMin = minCount <= 0 ? definedMin : minCount;
        int requestedMax = maxCount <= 0 ? definedMax : maxCount;

        int effectiveMin = Mathf.Clamp(requestedMin, definedMin, definedMax);
        int effectiveMax = Mathf.Clamp(requestedMax, definedMin, definedMax);
        if (effectiveMax < effectiveMin)
            effectiveMax = effectiveMin;

        List<DropCountOption> filtered = options
            .Where(o => o.count >= effectiveMin && o.count <= effectiveMax && o.weight > 0)
            .ToList();

        if (filtered.Count == 0)
            return effectiveMin;

        return PickCount(filtered);
    }

    public int PickCountInProfile(CountRangeWeightProfile profile, int minBonus = 0, int maxBonus = 0)
    {
        if (profile == null)
            return 0;

        return PickCountInRange(profile.weights, profile.minCount + minBonus, profile.maxCount + maxBonus);
    }

    public ItemRarity RollStageRelicRarity(StageLootTable table)
    {
        if (table == null)
            return ItemRarity.Common;

        int total = table.commonWeight + table.rareWeight + table.epicWeight;
        if (total <= 0)
            return ItemRarity.Common;

        int rand = Random.Range(0, total);
        int sum = 0;

        sum += table.commonWeight;
        if (rand < sum)
            return ItemRarity.Common;

        sum += table.rareWeight;
        if (rand < sum)
            return ItemRarity.Rare;

        return ItemRarity.Epic;
    }

    public MonsterLootType RollMonsterLootType(StageLootTable table)
    {
        if (table == null)
            return MonsterLootType.None;

        int totalWeight = table.mobNothingWeight + table.mobWeaponWeight + table.mobRelicWeight
            + table.mobConsumableWeight + table.mobFieldItemWeight;
        if (totalWeight <= 0)
            return MonsterLootType.None;

        int rand = Random.Range(0, totalWeight);
        int sum = 0;

        sum += table.mobNothingWeight;
        if (rand < sum)
            return MonsterLootType.None;

        sum += table.mobWeaponWeight;
        if (rand < sum)
            return MonsterLootType.Weapon;

        sum += table.mobRelicWeight;
        if (rand < sum)
            return MonsterLootType.Relic;

        sum += table.mobConsumableWeight;
        if (rand < sum)
            return MonsterLootType.Consumable;

        return MonsterLootType.FieldItem;
    }

    public ItemRarity RollGraveRelicRarity(GraveLootTable graveLootTable, float bonusRareChance, float bonusEpicChance)
    {
        if (graveLootTable == null)
            return ItemRarity.Common;

        float normalW = graveLootTable.normalRelicWeight;
        float rareW = graveLootTable.rareRelicWeight + bonusRareChance;
        float epicW = graveLootTable.epicRelicWeight + bonusEpicChance;

        float total = normalW + rareW + epicW;
        if (total <= 0f)
            return ItemRarity.Common;

        float rand = Random.Range(0f, total);
        if (rand < normalW)
            return ItemRarity.Common;

        if (rand < normalW + rareW)
            return ItemRarity.Rare;

        return ItemRarity.Epic;
    }
}
