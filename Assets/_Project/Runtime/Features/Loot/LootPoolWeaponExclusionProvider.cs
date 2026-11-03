using System.Collections.Generic;
using UnityEngine;

// 책임: 루팅 후보 제외 계산에 필요한 루팅 컨텍스트와 현재 제외 소스 집합을 묶어 전달한다.
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

// 책임: 플레이어/월드/씬/상인별로 이미 노출된 무기 ID 집합을 보관한다.
internal readonly struct LootPoolWeaponExclusionSourceSet
{
    private readonly HashSet<string> playerWeaponIds;
    private readonly HashSet<string> worldPickupWeaponIds;
    private readonly HashSet<string> sceneWeaponDropIds;
    private readonly HashSet<string> merchantWeaponIds;
    private readonly HashSet<string> openedChestWeaponIds;
    private readonly HashSet<string> unsoldMerchantWeaponIds;

    public LootPoolWeaponExclusionSourceSet(
        HashSet<string> playerWeaponIds,
        HashSet<string> worldPickupWeaponIds,
        HashSet<string> sceneWeaponDropIds,
        HashSet<string> merchantWeaponIds,
        HashSet<string> openedChestWeaponIds = null,
        HashSet<string> unsoldMerchantWeaponIds = null)
    {
        this.playerWeaponIds = Copy(playerWeaponIds);
        this.worldPickupWeaponIds = Copy(worldPickupWeaponIds);
        this.sceneWeaponDropIds = Copy(sceneWeaponDropIds);
        this.merchantWeaponIds = Copy(merchantWeaponIds);
        this.openedChestWeaponIds = Copy(openedChestWeaponIds);
        this.unsoldMerchantWeaponIds = Copy(unsoldMerchantWeaponIds);
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

    public void AddOpenedChestWeaponIdsTo(HashSet<string> exclusionList)
    {
        AddRange(exclusionList, openedChestWeaponIds);
    }

    public void AddUnsoldMerchantWeaponIdsTo(HashSet<string> exclusionList)
    {
        AddRange(exclusionList, unsoldMerchantWeaponIds);
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

// 책임: 루팅 후보에서 제외해야 할 무기 ID 결과 집합을 외부 변경 없이 반환한다.
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

// 책임: 루팅 컨텍스트가 요청한 출처들의 무기 ID를 통합해 제외 결과를 만든다.
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
        if (context.Includes(LootPoolExclusionSource.OpenedChestStock))
            sourceSet.AddOpenedChestWeaponIdsTo(exclusionList);
        if (context.Includes(LootPoolExclusionSource.UnsoldMerchantStock))
            sourceSet.AddUnsoldMerchantWeaponIdsTo(exclusionList);

        return new LootPoolWeaponExclusionResult(exclusionList);
    }
}

// 책임: 플레이어/월드/씬/상인 상태에서 현재 루팅 후보에서 제외할 무기 ID 집합을 수집한다.
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
        HashSet<string> openedChestWeaponIds = context.Includes(LootPoolExclusionSource.OpenedChestStock)
            ? CollectOpenedChestWeaponIds(context.SourceChest)
            : null;
        HashSet<string> unsoldMerchantWeaponIds = context.Includes(LootPoolExclusionSource.UnsoldMerchantStock)
            ? CollectMerchantWeaponIds(unsoldOnly: true)
            : null;

        return new LootPoolWeaponExclusionSourceSet(
            playerWeaponIds,
            worldPickupWeaponIds,
            sceneWeaponDropIds,
            merchantWeaponIds,
            openedChestWeaponIds,
            unsoldMerchantWeaponIds);
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

    private static HashSet<string> CollectMerchantWeaponIds(bool unsoldOnly = false)
    {
        var weaponIds = new HashSet<string>();

        GamePlayData data = RunSessionStore.Data;
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
                    (unsoldOnly && entry.isSold) ||
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

    private static HashSet<string> CollectOpenedChestWeaponIds(TreasureChest sourceChest)
    {
        var weaponIds = new HashSet<string>();
        TreasureChest[] chests = Object.FindObjectsByType<TreasureChest>(FindObjectsInactive.Include);
        foreach (TreasureChest chest in chests)
        {
            if (chest == null || chest == sourceChest || !chest.IsOpened || chest.IsLootSelectionComplete)
                continue;

            ChestInventory inventory = chest.GetInventory();
            if (inventory == null)
                continue;

            for (int i = 0; i < inventory.Capacity; i++)
                if (inventory.Get(i) is WeaponDefinition weapon)
                    AddWeaponId(weaponIds, weapon.weaponId);
        }

        // Loaded dungeons use live inventories; their old scene-exit snapshots
        // must not reserve consumed loot or the source chest's previous roll.
        var liveDungeonIds = new HashSet<string>();
        DungeonGenerator[] generators = Object.FindObjectsByType<DungeonGenerator>(FindObjectsInactive.Include);
        foreach (DungeonGenerator generator in generators)
            if (generator != null && generator.LastGenerationSucceeded &&
                generator.ReentryPolicy == DungeonReentryPolicy.PreserveDuringRun)
                liveDungeonIds.Add(generator.DungeonStateId);

        var savedDungeons = RunSessionStore.Data?.dungeonRunStates;
        if (savedDungeons == null)
            return weaponIds;

        foreach (DungeonRunStateData dungeon in savedDungeons)
        {
            if (dungeon?.objectStates == null || liveDungeonIds.Contains(dungeon.dungeonId))
                continue;

            foreach (DungeonObjectRuntimeStateData state in dungeon.objectStates)
            {
                if (state == null || !state.isPresent || !state.isChestOpened || state.chestLoot == null)
                    continue;

                foreach (DungeonChestLootRuntimeStateData loot in state.chestLoot)
                    if (loot?.item is WeaponDefinition weapon)
                        AddWeaponId(weaponIds, weapon.weaponId);
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
