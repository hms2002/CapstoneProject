using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class LootPoolItemSelectionService
{
    public static WeaponDefinition GetRandomWeapon(HashSet<string> exclusionList)
    {
        if (ItemManager.Instance == null)
            return null;

        exclusionList ??= new HashSet<string>();

        var pool = ItemManager.Instance.GetUnlockedWeaponIDs();
        var valid = pool.Where(w => !exclusionList.Contains(w)).ToList();
        if (valid.Count == 0)
            return null;

        string pickedID = valid[Random.Range(0, valid.Count)];
        return ItemManager.Instance.GetWeaponData(pickedID);
    }

    public static RelicDefinition GetRandomRelicByRarity(ItemRarity targetRarity)
    {
        if (ItemManager.Instance == null)
            return null;

        var pool = ItemManager.Instance.GetUnlockedRelicIDs();
        if (pool.Count == 0)
            return null;

        var allUnlockedRelics = new List<RelicDefinition>();
        var exactMatches = new List<RelicDefinition>();
        var lowerRarityMatches = new List<RelicDefinition>();

        foreach (var id in pool)
        {
            var relicData = ItemManager.Instance.GetRelicData(id);
            if (relicData == null)
                continue;

            allUnlockedRelics.Add(relicData);

            if (relicData.rarity == targetRarity)
                exactMatches.Add(relicData);
            else if (relicData.rarity < targetRarity)
                lowerRarityMatches.Add(relicData);
        }

        if (exactMatches.Count > 0)
            return exactMatches[Random.Range(0, exactMatches.Count)];

        if (lowerRarityMatches.Count > 0)
            return lowerRarityMatches[Random.Range(0, lowerRarityMatches.Count)];

        if (allUnlockedRelics.Count == 0)
            return null;

        return allUnlockedRelics[Random.Range(0, allUnlockedRelics.Count)];
    }

    public static ConsumableDefinition GetRandomConsumable()
    {
        if (ItemManager.Instance == null)
            return null;

        var pool = ItemManager.Instance.GetAllConsumables();
        if (pool == null || pool.Count == 0)
            return null;

        return pool[Random.Range(0, pool.Count)];
    }
}
