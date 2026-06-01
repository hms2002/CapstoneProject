using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>몬스터 처치 드롭을 굴릴 때 필요한 위치, 테이블, 드롭 주체, 제외 정책을 전달합니다.</summary>
internal readonly struct MonsterLootDropRequest
{
    public Vector3 Position { get; }
    public StageLootTable Table { get; }
    public GameObject Source { get; }
    public LootPoolContext WeaponExclusionContext { get; }
    public bool SuppressFieldItem { get; }

    public MonsterLootDropRequest(Vector3 position, StageLootTable table)
        : this(position, table, source: null, LootPoolContext.PlayerInventory, suppressFieldItem: false)
    {
    }

    public MonsterLootDropRequest(Vector3 position, StageLootTable table, bool suppressFieldItem)
        : this(position, table, source: null, LootPoolContext.PlayerInventory, suppressFieldItem)
    {
    }

    public MonsterLootDropRequest(Vector3 position, StageLootTable table, GameObject source)
        : this(position, table, source, LootPoolContext.PlayerInventory, suppressFieldItem: false)
    {
    }

    public MonsterLootDropRequest(Vector3 position, StageLootTable table, GameObject source, bool suppressFieldItem)
        : this(position, table, source, LootPoolContext.PlayerInventory, suppressFieldItem)
    {
    }

    public MonsterLootDropRequest(Vector3 position, StageLootTable table, LootPoolContext weaponExclusionContext)
        : this(position, table, source: null, weaponExclusionContext, suppressFieldItem: false)
    {
    }

    public MonsterLootDropRequest(
        Vector3 position,
        StageLootTable table,
        LootPoolContext weaponExclusionContext,
        bool suppressFieldItem)
        : this(position, table, source: null, weaponExclusionContext, suppressFieldItem)
    {
    }

    public MonsterLootDropRequest(Vector3 position, StageLootTable table, GameObject source, LootPoolContext weaponExclusionContext)
        : this(position, table, source, weaponExclusionContext, suppressFieldItem: false)
    {
    }

    public MonsterLootDropRequest(
        Vector3 position,
        StageLootTable table,
        GameObject source,
        LootPoolContext weaponExclusionContext,
        bool suppressFieldItem)
    {
        Position = position;
        Table = table;
        Source = source;
        WeaponExclusionContext = weaponExclusionContext;
        SuppressFieldItem = suppressFieldItem;
    }
}

/// <summary>몬스터 처치 드롭 롤 결과와 실제 생성 여부를 호출자에게 알려줍니다.</summary>
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

/// <summary>몬스터 처치 보상 타입을 굴리고 실제 월드 드롭 오브젝트 생성을 위임합니다.</summary>
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

        bool suppressFieldItem = request.SuppressFieldItem || ShouldSuppressFieldHealPickup(request.Source);
        MonsterLootType lootType = rollService.RollMonsterLootType(request.Table, suppressFieldItem);
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
                if (suppressFieldItem)
                    return new MonsterLootDropResult(lootType, false);

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

    private static bool ShouldSuppressFieldHealPickup(GameObject source)
    {
        return source != null && source.GetComponentInParent<Pawn>() != null;
    }
}
