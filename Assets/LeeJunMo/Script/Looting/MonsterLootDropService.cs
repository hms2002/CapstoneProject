using System;
using System.Collections.Generic;
using UnityEngine;

internal readonly struct MonsterLootDropRequest
{
    public Vector3 Position { get; }
    public StageLootTable Table { get; }
    public LootPoolContext WeaponExclusionContext { get; }

    public MonsterLootDropRequest(Vector3 position, StageLootTable table)
        : this(position, table, LootPoolContext.PlayerInventory)
    {
    }

    public MonsterLootDropRequest(Vector3 position, StageLootTable table, LootPoolContext weaponExclusionContext)
    {
        Position = position;
        Table = table;
        WeaponExclusionContext = weaponExclusionContext;
    }
}

internal readonly struct MonsterLootDropResult
{
    public static MonsterLootDropResult Empty => new MonsterLootDropResult(MonsterLootType.None, false);

    public MonsterLootType LootType { get; }
    public bool Spawned { get; }

    public MonsterLootDropResult(MonsterLootType lootType, bool spawned)
    {
        LootType = lootType;
        Spawned = spawned;
    }
}

internal sealed class MonsterLootDropService
{
    private readonly LootPoolService poolService;
    private readonly LootRollService rollService;
    private readonly LootSpawnService spawnService;
    private readonly Func<RelicDefinition> randomRelicProvider;

    public MonsterLootDropService(
        LootPoolService poolService,
        LootRollService rollService,
        LootSpawnService spawnService,
        Func<RelicDefinition> randomRelicProvider)
    {
        this.poolService = poolService;
        this.rollService = rollService;
        this.spawnService = spawnService;
        this.randomRelicProvider = randomRelicProvider;
    }

    public MonsterLootDropResult Spawn(MonsterLootDropRequest request)
    {
        if (request.Table == null || poolService == null || rollService == null || spawnService == null)
            return MonsterLootDropResult.Empty;

        MonsterLootType lootType = rollService.RollMonsterLootType(request.Table);
        switch (lootType)
        {
            case MonsterLootType.None:
                return new MonsterLootDropResult(lootType, false);

            case MonsterLootType.Weapon:
                return SpawnWeaponDrop(request.Position, request.WeaponExclusionContext, lootType);

            case MonsterLootType.Relic:
                return SpawnRelicDrop(request.Position, lootType);

            case MonsterLootType.Consumable:
                return SpawnConsumableDrop(request.Position, lootType);

            case MonsterLootType.FieldItem:
                spawnService.SpawnFieldHealPickup(request.Position);
                return new MonsterLootDropResult(lootType, true);

            default:
                return new MonsterLootDropResult(lootType, false);
        }
    }

    private MonsterLootDropResult SpawnWeaponDrop(Vector3 position, LootPoolContext weaponExclusionContext, MonsterLootType lootType)
    {
        HashSet<string> banList = poolService.BuildWeaponExclusionSet(weaponExclusionContext);
        WeaponDefinition weapon = poolService.GetRandomWeapon(banList);
        if (weapon == null)
            return new MonsterLootDropResult(lootType, false);

        spawnService.SpawnLootObject(position, weapon);
        return new MonsterLootDropResult(lootType, true);
    }

    private MonsterLootDropResult SpawnRelicDrop(Vector3 position, MonsterLootType lootType)
    {
        RelicDefinition relic = randomRelicProvider != null ? randomRelicProvider.Invoke() : null;
        if (relic == null)
            return new MonsterLootDropResult(lootType, false);

        spawnService.SpawnLootObject(position, relic);
        return new MonsterLootDropResult(lootType, true);
    }

    private MonsterLootDropResult SpawnConsumableDrop(Vector3 position, MonsterLootType lootType)
    {
        ConsumableDefinition consumable = poolService.GetRandomConsumable();
        if (consumable == null)
            return new MonsterLootDropResult(lootType, false);

        spawnService.SpawnLootObject(position, consumable);
        return new MonsterLootDropResult(lootType, true);
    }
}
