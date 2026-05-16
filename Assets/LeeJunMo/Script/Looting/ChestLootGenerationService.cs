using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class ChestLootGenerationService
{
    private readonly LootPoolService poolService;
    private readonly LootRollService rollService;
    private readonly Func<RelicDefinition> randomRelicProvider;

    public ChestLootGenerationService(
        LootPoolService poolService,
        LootRollService rollService,
        Func<RelicDefinition> randomRelicProvider)
    {
        this.poolService = poolService;
        this.rollService = rollService;
        this.randomRelicProvider = randomRelicProvider;
    }

    public ChestLootResult Generate(
        StageLootTable table,
        ChestLootRequest request,
        ChestRunModifierDelta chestModifiers)
    {
        if (table == null || poolService == null || rollService == null)
            return ChestLootResult.Empty;

        var drops = new List<ScriptableObject>();
        HashSet<string> banList = poolService.BuildWeaponExclusionSet(request.WeaponExclusionContext);

        int weaponCount = rollService.PickCountInProfile(
            table.ChestWeaponCountProfile,
            chestModifiers.chestWeaponMinBonus,
            chestModifiers.chestWeaponMaxBonus);
        for (int i = 0; i < weaponCount; i++)
        {
            WeaponDefinition weapon = poolService.GetRandomWeapon(banList);
            if (weapon == null)
                continue;

            drops.Add(weapon);
            banList.Add(weapon.weaponId);
        }

        int relicCount = rollService.PickCountInProfile(
            table.ChestRelicCountProfile,
            chestModifiers.chestRelicMinBonus,
            chestModifiers.chestRelicMaxBonus);
        for (int i = 0; i < relicCount; i++)
        {
            RelicDefinition relic = randomRelicProvider != null ? randomRelicProvider.Invoke() : null;
            if (relic != null)
                drops.Add(relic);
        }

        int consumableCount = rollService.PickCountInProfile(table.ChestConsumableCountProfile);
        for (int i = 0; i < consumableCount; i++)
        {
            ConsumableDefinition consumable = poolService.GetRandomConsumable();
            if (consumable != null)
                drops.Add(consumable);
        }

        return new ChestLootResult(drops);
    }
}

public readonly struct ChestLootRequest
{
    public static ChestLootRequest Default => new ChestLootRequest(default);

    private readonly LootPoolContext weaponExclusionContext;
    private readonly bool hasWeaponExclusionContext;

    public ChestRunModifierDelta ExtraModifiers { get; }
    public LootPoolContext WeaponExclusionContext =>
        hasWeaponExclusionContext ? weaponExclusionContext : LootPoolContext.PlayerInventory;

    public ChestLootRequest(ChestRunModifierDelta extraModifiers)
        : this(extraModifiers, LootPoolContext.PlayerInventory)
    {
    }

    public ChestLootRequest(ChestRunModifierDelta extraModifiers, LootPoolContext weaponExclusionContext)
    {
        ExtraModifiers = extraModifiers;
        this.weaponExclusionContext = weaponExclusionContext;
        hasWeaponExclusionContext = true;
    }
}

public readonly struct ChestLootResult
{
    public static ChestLootResult Empty => new ChestLootResult(null);

    private readonly List<ScriptableObject> items;

    public IReadOnlyList<ScriptableObject> Items => items != null ? items : Array.Empty<ScriptableObject>();

    public ChestLootResult(List<ScriptableObject> items)
    {
        this.items = items ?? new List<ScriptableObject>();
    }

    public List<ScriptableObject> ToList()
    {
        return items != null ? new List<ScriptableObject>(items) : new List<ScriptableObject>();
    }
}
