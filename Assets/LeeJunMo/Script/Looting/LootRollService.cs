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
