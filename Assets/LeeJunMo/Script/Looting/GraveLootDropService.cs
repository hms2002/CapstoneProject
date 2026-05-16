using System;
using System.Collections.Generic;
using UnityEngine;

internal readonly struct GraveLootDropRequest
{
    public Vector3 Position { get; }
    public GraveType Type { get; }
    public GraveLootTable Table { get; }
    public int BonusMinCount { get; }
    public int BonusMaxCount { get; }
    public float BonusRareChance { get; }
    public float BonusEpicChance { get; }

    public GraveLootDropRequest(
        Vector3 position,
        GraveType type,
        GraveLootTable table,
        int bonusMinCount = 0,
        int bonusMaxCount = 0,
        float bonusRareChance = 0f,
        float bonusEpicChance = 0f)
    {
        Position = position;
        Type = type;
        Table = table;
        BonusMinCount = bonusMinCount;
        BonusMaxCount = bonusMaxCount;
        BonusRareChance = bonusRareChance;
        BonusEpicChance = bonusEpicChance;
    }
}

internal readonly struct GraveLootDropResult
{
    public static GraveLootDropResult Empty => new GraveLootDropResult(default, 0);

    public GraveType Type { get; }
    public int SpawnedCount { get; }

    public GraveLootDropResult(GraveType type, int spawnedCount)
    {
        Type = type;
        SpawnedCount = Mathf.Max(0, spawnedCount);
    }
}

internal sealed class GraveLootDropService
{
    private readonly LootPoolService poolService;
    private readonly LootRollService rollService;
    private readonly LootSpawnService spawnService;
    private readonly Func<ItemRarity, RelicDefinition> relicByRarityProvider;

    public GraveLootDropService(
        LootPoolService poolService,
        LootRollService rollService,
        LootSpawnService spawnService,
        Func<ItemRarity, RelicDefinition> relicByRarityProvider)
    {
        this.poolService = poolService;
        this.rollService = rollService;
        this.spawnService = spawnService;
        this.relicByRarityProvider = relicByRarityProvider;
    }

    public GraveLootDropResult Spawn(GraveLootDropRequest request)
    {
        if (request.Table == null || poolService == null || rollService == null || spawnService == null)
            return GraveLootDropResult.Empty;

        switch (request.Type)
        {
            case GraveType.Weapon:
                return SpawnWeaponGraveLoot(request);

            case GraveType.Relic:
                return SpawnRelicGraveLoot(request);

            default:
                return new GraveLootDropResult(request.Type, 0);
        }
    }

    private GraveLootDropResult SpawnWeaponGraveLoot(GraveLootDropRequest request)
    {
        int totalCount = rollService.PickCountInProfile(
            request.Table.WeaponDropCountProfile,
            request.BonusMinCount,
            request.BonusMaxCount);
        List<Vector3> landingPositions = spawnService.GetHorizontalGroundPositions(request.Position, 1);
        HashSet<string> banList = poolService.BuildWeaponExclusionSet(LootPoolContext.PlayerInventoryAndMerchantStock);
        int spawnedCount = 0;

        for (int i = 0; i < totalCount; i++)
        {
            WeaponDefinition weapon = poolService.GetRandomWeapon(banList);
            if (weapon == null)
                continue;

            SpawnAnimatedGraveLoot(request.Position, landingPositions, i, weapon);
            spawnedCount++;

            if (!string.IsNullOrWhiteSpace(weapon.weaponId))
                banList.Add(weapon.weaponId);
        }

        return new GraveLootDropResult(request.Type, spawnedCount);
    }

    private GraveLootDropResult SpawnRelicGraveLoot(GraveLootDropRequest request)
    {
        int totalCount = rollService.PickCountInProfile(
            request.Table.RelicDropCountProfile,
            request.BonusMinCount,
            request.BonusMaxCount);
        List<Vector3> landingPositions = spawnService.GetHorizontalGroundPositions(request.Position, 1);
        int spawnedCount = 0;

        for (int i = 0; i < totalCount; i++)
        {
            ItemRarity rarity = rollService.RollGraveRelicRarity(
                request.Table,
                request.BonusRareChance,
                request.BonusEpicChance);
            RelicDefinition relic = relicByRarityProvider != null ? relicByRarityProvider.Invoke(rarity) : null;
            if (relic == null)
                continue;

            SpawnAnimatedGraveLoot(request.Position, landingPositions, i, relic);
            spawnedCount++;
        }

        return new GraveLootDropResult(request.Type, spawnedCount);
    }

    private void SpawnAnimatedGraveLoot(Vector3 originPosition, List<Vector3> landingPositions, int dropIndex, ScriptableObject itemData)
    {
        Vector3 landingPosition = landingPositions != null && landingPositions.Count > 0
            ? landingPositions[dropIndex % landingPositions.Count]
            : originPosition + spawnService.GetRandomScatterOffset();

        spawnService.SpawnAnimatedLootObject(originPosition, landingPosition, itemData);
    }
}
