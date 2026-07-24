using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class ChestLootGenerationService
{
    private readonly LootPoolService poolService;
    private readonly LootRollService rollService;
    private readonly Func<RelicDefinition> randomRelicProvider;
    private readonly Func<ItemRarity, RelicDefinition> relicByRarityProvider;

    public ChestLootGenerationService(
        LootPoolService poolService,
        LootRollService rollService,
        Func<RelicDefinition> randomRelicProvider,
        Func<ItemRarity, RelicDefinition> relicByRarityProvider)
    {
        this.poolService = poolService;
        this.rollService = rollService;
        this.randomRelicProvider = randomRelicProvider;
        this.relicByRarityProvider = relicByRarityProvider;
    }

    public ChestLootResult Generate(
        StageLootTable table,
        ChestLootRequest request,
        ChestRunModifierDelta chestModifiers)
    {
        if (table == null || poolService == null || rollService == null)
            return ChestLootResult.Empty;

        return GenerateCore(
            table.ChestWeaponCountProfile,
            table.ChestRelicCountProfile,
            table.ChestConsumableCountProfile,
            request,
            chestModifiers,
            () => randomRelicProvider != null ? randomRelicProvider.Invoke() : null);
    }

    public ChestLootResult GenerateBoss(
        StageLootTable table,
        ChestLootRequest request,
        ChestRunModifierDelta bossChestModifiers)
    {
        if (table == null || poolService == null || rollService == null)
            return ChestLootResult.Empty;

        return GenerateCore(
            table.BossWeaponCountProfile,
            table.BossRelicCountProfile,
            null,
            request,
            bossChestModifiers,
            () =>
            {
                ItemRarity rarity = rollService.RollBossRelicRarity(table);
                return relicByRarityProvider != null ? relicByRarityProvider.Invoke(rarity) : null;
            });
    }

    private ChestLootResult GenerateCore(
        CountRangeWeightProfile weaponCountProfile,
        CountRangeWeightProfile relicCountProfile,
        CountRangeWeightProfile consumableCountProfile,
        ChestLootRequest request,
        ChestRunModifierDelta chestModifiers,
        Func<RelicDefinition> relicProvider)
    {
        if (request.HasOverrideProfile)
            return GenerateOverrideCore(request, relicProvider);

        var drops = new List<ScriptableObject>();
        HashSet<string> banList = poolService.BuildWeaponExclusionSet(request.WeaponExclusionContext);

        int weaponCount = rollService.PickCountInProfile(
            weaponCountProfile,
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
            relicCountProfile,
            chestModifiers.chestRelicMinBonus,
            chestModifiers.chestRelicMaxBonus);
        for (int i = 0; i < relicCount; i++)
        {
            RelicDefinition relic = relicProvider != null ? relicProvider.Invoke() : null;
            if (relic != null)
                drops.Add(relic);
        }

        int consumableCount = rollService.PickCountInProfile(consumableCountProfile);
        for (int i = 0; i < consumableCount; i++)
        {
            ConsumableDefinition consumable = poolService.GetRandomConsumable();
            if (consumable != null)
                drops.Add(consumable);
        }

        return new ChestLootResult(drops);
    }

    private ChestLootResult GenerateOverrideCore(ChestLootRequest request, Func<RelicDefinition> relicProvider)
    {
        ChestLootOverrideProfile profile = request.OverrideProfile;
        var drops = new List<ScriptableObject>();
        HashSet<string> banList = poolService.BuildWeaponExclusionSet(request.WeaponExclusionContext);

        int weaponCount = rollService.PickCountInProfile(profile.WeaponCountProfile);
        for (int i = 0; i < weaponCount; i++)
        {
            WeaponDefinition weapon = poolService.GetRandomWeapon(banList);
            if (weapon == null)
                continue;

            drops.Add(weapon);
            banList.Add(weapon.weaponId);
        }

        int relicCount = rollService.PickCountInProfile(profile.RelicCountProfile);
        for (int i = 0; i < relicCount; i++)
        {
            RelicDefinition relic = relicProvider != null ? relicProvider.Invoke() : null;
            if (relic != null)
                drops.Add(relic);
        }

        int consumableCount = profile.ResolveConsumableCount(drops.Count, rollService);
        for (int i = 0; i < consumableCount; i++)
        {
            ConsumableDefinition consumable = poolService.GetRandomConsumable();
            if (consumable != null)
                drops.Add(consumable);
        }

        return new ChestLootResult(drops);
    }
}

/// <summary>
/// 책임: 상자 프리팹이 스테이지 루트 테이블을 쓸지, 프리팹 전용 수량 정책을 쓸지 선택한다.
/// </summary>
public enum ChestLootMode
{
    StageTable,
    OverrideProfile
}

/// <summary>
/// 책임: 특정 상자 프리팹이 스테이지 테이블 대신 사용할 보상 수량 규칙을 보관한다.
/// </summary>
[Serializable]
public sealed class ChestLootOverrideProfile
{
    [SerializeField] private int totalLootCount = 8;
    [SerializeField] private CountRangeWeightProfile weaponCountProfile = CreateFixedCountProfile(0);
    [SerializeField] private CountRangeWeightProfile relicCountProfile = CreateWeightedCountProfile(7, 8);
    [SerializeField] private CountRangeWeightProfile consumableCountProfile = CreateFixedCountProfile(0);
    [SerializeField] private bool fillRemainingWithConsumables = true;

    public CountRangeWeightProfile WeaponCountProfile => weaponCountProfile;
    public CountRangeWeightProfile RelicCountProfile => relicCountProfile;

    public int ResolveConsumableCount(int currentLootCount, LootRollService rollService)
    {
        if (fillRemainingWithConsumables)
            return Mathf.Max(0, totalLootCount - currentLootCount);

        return rollService != null ? rollService.PickCountInProfile(consumableCountProfile) : 0;
    }

    private static CountRangeWeightProfile CreateFixedCountProfile(int count)
    {
        return new CountRangeWeightProfile
        {
            minCount = count,
            maxCount = count,
            weights = new List<DropCountOption>
            {
                new DropCountOption { count = count, weight = 100 }
            }
        };
    }

    private static CountRangeWeightProfile CreateWeightedCountProfile(int minCount, int maxCount)
    {
        return new CountRangeWeightProfile
        {
            minCount = minCount,
            maxCount = maxCount,
            weights = new List<DropCountOption>
            {
                new DropCountOption { count = minCount, weight = 100 },
                new DropCountOption { count = maxCount, weight = 100 }
            }
        };
    }
}

public readonly struct ChestLootRequest
{
    public static ChestLootRequest Default => new ChestLootRequest(default);

    private readonly LootPoolContext weaponExclusionContext;
    private readonly bool hasWeaponExclusionContext;

    public ChestRunModifierDelta ExtraModifiers { get; }
    public ChestLootOverrideProfile OverrideProfile { get; }
    public bool HasOverrideProfile => OverrideProfile != null;
    public LootPoolContext WeaponExclusionContext =>
        hasWeaponExclusionContext ? weaponExclusionContext : LootPoolContext.PlayerInventory;

    public ChestLootRequest(ChestRunModifierDelta extraModifiers)
        : this(extraModifiers, LootPoolContext.PlayerInventory)
    {
    }

    public ChestLootRequest(ChestRunModifierDelta extraModifiers, LootPoolContext weaponExclusionContext)
        : this(extraModifiers, weaponExclusionContext, null)
    {
    }

    public ChestLootRequest(
        ChestRunModifierDelta extraModifiers,
        LootPoolContext weaponExclusionContext,
        ChestLootOverrideProfile overrideProfile)
    {
        ExtraModifiers = extraModifiers;
        OverrideProfile = overrideProfile;
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
