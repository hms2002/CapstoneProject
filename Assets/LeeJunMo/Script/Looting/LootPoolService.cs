using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class LootPoolService
{
    public HashSet<string> BuildPlayerWeaponExclusionSet()
    {
        var exclusionList = new HashSet<string>();
        var currentPlayer = PlayerRuntimeRegistry.CurrentPlayer != null
            ? PlayerRuntimeRegistry.CurrentPlayer
            : PlayerInteractor2D.Instance;

        if (currentPlayer == null)
            return exclusionList;

        WeaponInventory2D weaponInventory = currentPlayer.GetComponent<WeaponInventory2D>();
        if (weaponInventory != null)
            exclusionList.UnionWith(weaponInventory.GetAllWeaponIDs());

        return exclusionList;
    }

    public HashSet<string> BuildShopWeaponExclusionSet()
    {
        HashSet<string> exclusionList = BuildPlayerWeaponExclusionSet();
        AddWorldItemWeaponExclusions(exclusionList);
        AddWeaponDropExclusions(exclusionList);
        return exclusionList;
    }

    public HashSet<string> BuildMerchantWeaponExclusionSet()
    {
        var exclusionList = new HashSet<string>();
        GamePlayData data = GamePlayDataManager.Instance != null ? GamePlayDataManager.Instance.Data : null;
        if (data?.merchantStates == null)
            return exclusionList;

        for (int i = 0; i < data.merchantStates.Count; i++)
        {
            MerchantRuntimeState merchantState = data.merchantStates[i];
            if (merchantState?.slots == null)
                continue;

            for (int j = 0; j < merchantState.slots.Count; j++)
            {
                MerchantStockEntryState entry = merchantState.slots[j];
                if (entry == null ||
                    entry.kind != InventoryItemKind.Weapon ||
                    string.IsNullOrWhiteSpace(entry.itemId))
                {
                    continue;
                }

                exclusionList.Add(entry.itemId);
            }
        }

        return exclusionList;
    }

    private static void AddWorldItemWeaponExclusions(HashSet<string> exclusionList)
    {
        if (exclusionList == null)
            return;

        IReadOnlyList<WorldItemPickup2D> worldItems = WorldItemRegistry.Items;
        if (worldItems == null)
            return;

        for (int i = 0; i < worldItems.Count; i++)
        {
            if (worldItems[i] != null && worldItems[i].Item is WeaponDefinition weapon)
                AddWeaponId(exclusionList, weapon.weaponId);
        }
    }

    private static void AddWeaponDropExclusions(HashSet<string> exclusionList)
    {
        if (exclusionList == null)
            return;

        WeaponDrop2D[] drops = Object.FindObjectsByType<WeaponDrop2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < drops.Length; i++)
        {
            WeaponDefinition weapon = drops[i] != null ? drops[i].Weapon : null;
            if (weapon != null)
                AddWeaponId(exclusionList, weapon.weaponId);
        }
    }

    private static void AddWeaponId(HashSet<string> exclusionList, string weaponId)
    {
        if (!string.IsNullOrWhiteSpace(weaponId))
            exclusionList.Add(weaponId);
    }

    public WeaponDefinition GetRandomWeapon(HashSet<string> exclusionList)
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

    public RelicDefinition GetRandomRelicByRarity(ItemRarity targetRarity)
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

    public ConsumableDefinition GetRandomConsumable()
    {
        if (ItemManager.Instance == null)
            return null;

        var pool = ItemManager.Instance.GetAllConsumables();
        if (pool == null || pool.Count == 0)
            return null;

        return pool[Random.Range(0, pool.Count)];
    }
}
