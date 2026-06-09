using System.Collections.Generic;
using UnityEngine;

internal readonly struct LootPoolWeaponExclusionRequest
{
    public LootPoolContext Context { get; }
    public LootPoolWeaponExclusionSourceSet SourceSet { get; }

    public LootPoolWeaponExclusionRequest(
        LootPoolContext context,
        LootPoolWeaponExclusionSourceSet sourceSet)
    {
        Context = context;
        SourceSet = sourceSet;
    }
}

internal readonly struct LootPoolWeaponExclusionSourceSet
{
    private readonly HashSet<string> playerWeaponIds;
    private readonly HashSet<string> worldPickupWeaponIds;
    private readonly HashSet<string> sceneWeaponDropIds;
    private readonly HashSet<string> merchantWeaponIds;

    public LootPoolWeaponExclusionSourceSet(
        HashSet<string> playerWeaponIds,
        HashSet<string> worldPickupWeaponIds,
        HashSet<string> sceneWeaponDropIds,
        HashSet<string> merchantWeaponIds)
    {
        this.playerWeaponIds = Copy(playerWeaponIds);
        this.worldPickupWeaponIds = Copy(worldPickupWeaponIds);
        this.sceneWeaponDropIds = Copy(sceneWeaponDropIds);
        this.merchantWeaponIds = Copy(merchantWeaponIds);
    }

    public void AddPlayerWeaponIdsTo(HashSet<string> exclusionList)
    {
        AddRange(exclusionList, playerWeaponIds);
    }

    public void AddWorldPickupWeaponIdsTo(HashSet<string> exclusionList)
    {
        AddRange(exclusionList, worldPickupWeaponIds);
    }

    public void AddSceneWeaponDropIdsTo(HashSet<string> exclusionList)
    {
        AddRange(exclusionList, sceneWeaponDropIds);
    }

    public void AddMerchantWeaponIdsTo(HashSet<string> exclusionList)
    {
        AddRange(exclusionList, merchantWeaponIds);
    }

    private static HashSet<string> Copy(HashSet<string> weaponIds)
    {
        return weaponIds != null ? new HashSet<string>(weaponIds) : new HashSet<string>();
    }

    private static void AddRange(HashSet<string> exclusionList, HashSet<string> weaponIds)
    {
        if (exclusionList == null || weaponIds == null)
            return;

        exclusionList.UnionWith(weaponIds);
    }
}

internal readonly struct LootPoolWeaponExclusionResult
{
    private readonly HashSet<string> weaponIds;

    public LootPoolWeaponExclusionResult(HashSet<string> weaponIds)
    {
        this.weaponIds = weaponIds ?? new HashSet<string>();
    }

    public HashSet<string> ToHashSet()
    {
        return weaponIds != null ? new HashSet<string>(weaponIds) : new HashSet<string>();
    }
}

internal static class LootPoolWeaponExclusionProvider
{
    public static LootPoolWeaponExclusionResult Collect(LootPoolWeaponExclusionRequest request)
    {
        var exclusionList = new HashSet<string>();
        LootPoolContext context = request.Context;
        LootPoolWeaponExclusionSourceSet sourceSet = request.SourceSet;

        if (context.Includes(LootPoolExclusionSource.PlayerInventory))
            sourceSet.AddPlayerWeaponIdsTo(exclusionList);
        if (context.Includes(LootPoolExclusionSource.WorldPickups))
            sourceSet.AddWorldPickupWeaponIdsTo(exclusionList);
        if (context.Includes(LootPoolExclusionSource.SceneWeaponDrops))
            sourceSet.AddSceneWeaponDropIdsTo(exclusionList);
        if (context.Includes(LootPoolExclusionSource.MerchantStock))
            sourceSet.AddMerchantWeaponIdsTo(exclusionList);

        return new LootPoolWeaponExclusionResult(exclusionList);
    }
}

internal static class LootPoolLiveWeaponExclusionSourceProvider
{
    public static LootPoolWeaponExclusionSourceSet Collect(LootPoolContext context)
    {
        HashSet<string> playerWeaponIds = context.Includes(LootPoolExclusionSource.PlayerInventory)
            ? CollectPlayerWeaponIds()
            : null;
        HashSet<string> worldPickupWeaponIds = context.Includes(LootPoolExclusionSource.WorldPickups)
            ? CollectWorldPickupWeaponIds()
            : null;
        HashSet<string> sceneWeaponDropIds = context.Includes(LootPoolExclusionSource.SceneWeaponDrops)
            ? CollectSceneWeaponDropIds()
            : null;
        HashSet<string> merchantWeaponIds = context.Includes(LootPoolExclusionSource.MerchantStock)
            ? CollectMerchantWeaponIds()
            : null;

        return new LootPoolWeaponExclusionSourceSet(
            playerWeaponIds,
            worldPickupWeaponIds,
            sceneWeaponDropIds,
            merchantWeaponIds);
    }

    private static HashSet<string> CollectPlayerWeaponIds()
    {
        var weaponIds = new HashSet<string>();

        var currentPlayer = PlayerRuntimeRegistry.CurrentPlayer != null
            ? PlayerRuntimeRegistry.CurrentPlayer
            : PlayerInteractor2D.Instance;

        if (currentPlayer == null)
            return weaponIds;

        WeaponInventory2D weaponInventory = currentPlayer.GetComponent<WeaponInventory2D>();
        if (weaponInventory != null)
            weaponIds.UnionWith(weaponInventory.GetAllWeaponIDs());

        return weaponIds;
    }

    private static HashSet<string> CollectMerchantWeaponIds()
    {
        var weaponIds = new HashSet<string>();

        GamePlayData data = GamePlayDataManager.Instance != null ? GamePlayDataManager.Instance.Data : null;
        if (data?.merchantStates == null)
            return weaponIds;

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

                weaponIds.Add(entry.itemId);
            }
        }

        return weaponIds;
    }

    private static HashSet<string> CollectWorldPickupWeaponIds()
    {
        var weaponIds = new HashSet<string>();

        IReadOnlyList<WorldItemPickup2D> worldItems = WorldItemRegistry.Items;
        if (worldItems == null)
            return weaponIds;

        for (int i = 0; i < worldItems.Count; i++)
        {
            if (worldItems[i] != null && worldItems[i].Item is WeaponDefinition weapon)
                AddWeaponId(weaponIds, weapon.weaponId);
        }

        return weaponIds;
    }

    private static HashSet<string> CollectSceneWeaponDropIds()
    {
        var weaponIds = new HashSet<string>();

        WeaponDrop2D[] drops = Object.FindObjectsByType<WeaponDrop2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < drops.Length; i++)
        {
            WeaponDefinition weapon = drops[i] != null ? drops[i].Weapon : null;
            if (weapon != null)
                AddWeaponId(weaponIds, weapon.weaponId);
        }

        return weaponIds;
    }

    private static void AddWeaponId(HashSet<string> weaponIds, string weaponId)
    {
        if (!string.IsNullOrWhiteSpace(weaponId))
            weaponIds.Add(weaponId);
    }
}
