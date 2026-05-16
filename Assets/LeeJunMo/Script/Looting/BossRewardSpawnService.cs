using System.Collections.Generic;
using UnityEngine;

internal readonly struct BossRewardSpawnRequest
{
    public BossRewardContext Context { get; }
    public BossSpecialRewardPresetSO SpecialRewardPreset { get; }
    public GameObject ChestPrefab { get; }
    public Transform ChestSpawnPoint { get; }
    public GameObject MagicStonePrefab { get; }
    public Transform ScatterOrigin { get; }
    public float ScatterRadius { get; }
    public Object LogContext { get; }

    public BossRewardSpawnRequest(
        BossRewardContext context,
        BossSpecialRewardPresetSO specialRewardPreset,
        GameObject chestPrefab,
        Transform chestSpawnPoint,
        GameObject magicStonePrefab,
        Transform scatterOrigin,
        float scatterRadius,
        Object logContext)
    {
        Context = context;
        SpecialRewardPreset = specialRewardPreset;
        ChestPrefab = chestPrefab;
        ChestSpawnPoint = chestSpawnPoint;
        MagicStonePrefab = magicStonePrefab;
        ScatterOrigin = scatterOrigin;
        ScatterRadius = scatterRadius;
        LogContext = logContext;
    }
}

internal readonly struct BossRewardSpawnResult
{
    public static BossRewardSpawnResult Empty => new BossRewardSpawnResult(false, false, false);

    public bool ChestSpawned { get; }
    public bool CurrencySpawned { get; }
    public bool FieldHealsSpawned { get; }
    public bool SpawnedAny => ChestSpawned || CurrencySpawned || FieldHealsSpawned;

    public BossRewardSpawnResult(bool chestSpawned, bool currencySpawned, bool fieldHealsSpawned)
    {
        ChestSpawned = chestSpawned;
        CurrencySpawned = currencySpawned;
        FieldHealsSpawned = fieldHealsSpawned;
    }
}

internal static class BossRewardSpawnService
{
    public static BossRewardSpawnResult Spawn(BossRewardSpawnRequest request)
    {
        bool chestSpawned = TryRunRewardStep(
            () => SpawnTreasureChest(
                request.Context,
                request.SpecialRewardPreset,
                request.ChestPrefab,
                request.ChestSpawnPoint),
            "SpawnTreasureChest",
            request.LogContext);
        bool currencySpawned = TryRunRewardStep(
            () => SpawnBossCurrency(
                request.Context,
                request.MagicStonePrefab,
                request.ScatterOrigin,
                request.ScatterRadius),
            "SpawnBossCurrency",
            request.LogContext);
        bool fieldHealsSpawned = TryRunRewardStep(
            () => SpawnBossFieldHeals(
                request.Context,
                request.ScatterOrigin,
                request.ScatterRadius),
            "SpawnBossFieldHeals",
            request.LogContext);

        return new BossRewardSpawnResult(chestSpawned, currencySpawned, fieldHealsSpawned);
    }

    private static bool SpawnTreasureChest(
        BossRewardContext context,
        BossSpecialRewardPresetSO specialRewardPreset,
        GameObject resolvedChestPrefab,
        Transform resolvedChestSpawnPoint)
    {
        if (resolvedChestPrefab == null)
            return false;

        if (resolvedChestSpawnPoint == null)
            return false;

        Vector3 spawnPosition = resolvedChestSpawnPoint.position;
        GameObject chestObject = Object.Instantiate(resolvedChestPrefab, spawnPosition, Quaternion.identity);
        TreasureChest chest = chestObject.GetComponent<TreasureChest>();
        if (chest == null)
            return true;

        var finalLoots = new List<ScriptableObject>();
        BossRewardModifierAggregate modifiers = ResolveRewardModifiers(context);

        if (LootManager.Instance != null)
        {
            List<ScriptableObject> baseLoots = LootManager.Instance.GenerateBossChestLoot(modifiers.ChestModifierDelta);
            if (baseLoots != null)
                finalLoots.AddRange(baseLoots);
        }

        AddRolledBonusLoots(finalLoots, specialRewardPreset != null ? specialRewardPreset.SpecialLoots : null);
        AddRolledBonusLoots(finalLoots, modifiers.BonusLoots);
        chest.InitializeWithLoot(finalLoots);
        return true;
    }

    private static bool SpawnBossCurrency(
        BossRewardContext context,
        GameObject resolvedMagicStonePrefab,
        Transform scatterOrigin,
        float resolvedScatterRadius)
    {
        BossRewardModifierAggregate modifiers = ResolveRewardModifiers(context);
        int count = LootManager.Instance != null ? LootManager.Instance.GetBossMagicStoneCount() : 0;
        count += modifiers.MagicStoneBonus;
        if (count <= 0 || resolvedMagicStonePrefab == null)
            return false;

        if (scatterOrigin == null)
            return false;

        Vector3 origin = scatterOrigin.position;
        float radius = Mathf.Max(0f, resolvedScatterRadius);
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = origin + (Vector3)(Random.insideUnitCircle * radius);
            GameObject stoneObject = Object.Instantiate(resolvedMagicStonePrefab, spawnPosition, Quaternion.identity);
            MagicStonePickup pickup = stoneObject.GetComponent<MagicStonePickup>();
            if (pickup != null)
                pickup.amount = 1;
        }

        return true;
    }

    private static bool SpawnBossFieldHeals(
        BossRewardContext context,
        Transform scatterOrigin,
        float resolvedScatterRadius)
    {
        BossRewardModifierAggregate modifiers = ResolveRewardModifiers(context);
        int count = LootManager.Instance != null ? LootManager.Instance.GetBossFieldHealBaseCount() : 0;
        count += Mathf.Max(0, modifiers.FieldHealPickupBonus);
        if (count <= 0 || LootManager.Instance == null)
            return false;

        if (scatterOrigin == null)
            return false;

        Vector3 origin = scatterOrigin.position;
        float radius = Mathf.Max(0f, resolvedScatterRadius);
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = origin + (Vector3)(Random.insideUnitCircle * radius);
            LootManager.Instance.SpawnFieldHealPickup(spawnPosition);
        }

        return true;
    }

    private static BossRewardModifierAggregate ResolveRewardModifiers(BossRewardContext context)
    {
        return context != null ? context.RewardModifiers : default;
    }

    private static void AddRolledBonusLoots(List<ScriptableObject> finalLoots, IReadOnlyList<BossSpecificLoot> entries)
    {
        if (finalLoots == null || entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            BossSpecificLoot entry = entries[i];
            if (entry.item != null && Random.Range(0, 100) < entry.dropChance)
                finalLoots.Add(entry.item);
        }
    }

    private static bool TryRunRewardStep(System.Func<bool> action, string stepName, Object logContext)
    {
        if (action == null)
            return false;

        try
        {
            return action.Invoke();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(new System.Exception($"[BossRewardSpawner] {stepName} failed.", exception), logContext);
            return false;
        }
    }
}
