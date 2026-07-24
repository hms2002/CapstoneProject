using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 책임: 현재 해금/후보 목록과 제외 목록을 기준으로 loot pool에서 실제 아이템 정의를 선택한다.
/// </summary>
internal static class LootPoolItemSelectionService
{
    private static readonly HashSet<string> NonDroppableRelicIds = new HashSet<string>
    {
        "RD_RunningLedger",
        "RD_FeatherOrbit",
    };

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

    public static WeaponDefinition GetRandomWeaponFromCandidates(
        IReadOnlyList<WeaponDefinition> candidates,
        HashSet<string> exclusionList)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        exclusionList ??= new HashSet<string>();
        var valid = new List<WeaponDefinition>();
        for (int i = 0; i < candidates.Count; i++)
        {
            WeaponDefinition weapon = candidates[i];
            if (weapon == null || string.IsNullOrWhiteSpace(weapon.weaponId))
                continue;

            if (exclusionList.Contains(weapon.weaponId))
                continue;

            valid.Add(weapon);
        }

        if (valid.Count == 0)
            return null;

        return valid[Random.Range(0, valid.Count)];
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
            if (NonDroppableRelicIds.Contains(id))
                continue;

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
