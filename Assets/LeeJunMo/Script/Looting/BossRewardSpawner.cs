using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossRewardSpawner : MonoBehaviour
{
    [Header("Chest")]
    [SerializeField] private GameObject chestPrefab;
    [SerializeField] private Transform chestSpawnPoint;

    [Header("Currency")]
    [SerializeField] private GameObject magicStonePrefab;
    [SerializeField, Min(0f)] private float scatterRadius = 1.5f;

    [Header("Compatibility")]
    [SerializeField] private bool useBossDropReferencesIfMissing = true;

    private BossControllerBase owner;
    private BossDrop legacyDrop;
    private bool hasSpawned;

    private void Awake()
    {
        owner = GetComponentInParent<BossControllerBase>();
        legacyDrop = GetComponentInParent<BossDrop>();
    }

    private void OnEnable()
    {
        RunProgressCoordinator coordinator = RunProgressCoordinator.EnsureInstance();
        if (coordinator != null)
            coordinator.BossRewardsReady += HandleBossRewardsReady;
    }

    private void OnDisable()
    {
        if (RunProgressCoordinator.Instance != null)
            RunProgressCoordinator.Instance.BossRewardsReady -= HandleBossRewardsReady;
    }

    private void HandleBossRewardsReady(BossRewardContext context)
    {
        if (!CanHandle(context))
            return;

        BossDrop referenceDrop = useBossDropReferencesIfMissing ? ResolveLegacyDrop(context) : null;
        GameObject resolvedChestPrefab = chestPrefab != null ? chestPrefab : referenceDrop != null ? referenceDrop.chestPrefab : null;
        Transform resolvedChestSpawnPoint = chestSpawnPoint != null ? chestSpawnPoint : referenceDrop != null ? referenceDrop.chestSpawnPoint : null;
        GameObject resolvedMagicStonePrefab = magicStonePrefab != null ? magicStonePrefab : referenceDrop != null ? referenceDrop.magicStonePrefab : null;
        Transform scatterOrigin = ResolveScatterOrigin(context, referenceDrop);

        bool spawned = SpawnRewardsCore(
            context,
            resolvedChestPrefab,
            resolvedChestSpawnPoint,
            resolvedMagicStonePrefab,
            scatterOrigin,
            scatterRadius,
            referenceDrop != null ? referenceDrop.bossUniqueLoots : null,
            this);

        if (!spawned)
            return;

        hasSpawned = true;
        context.MarkRewardsHandled();
    }

    public static bool SpawnFromLegacyDrop(BossDrop legacyDrop, BossRewardContext context)
    {
        if (legacyDrop == null)
            return false;

        return SpawnRewardsCore(
            context,
            legacyDrop.chestPrefab,
            legacyDrop.chestSpawnPoint,
            legacyDrop.magicStonePrefab,
            legacyDrop.transform,
            1.5f,
            legacyDrop.bossUniqueLoots,
            legacyDrop);
    }

    private bool CanHandle(BossRewardContext context)
    {
        if (hasSpawned || context == null || context.RewardsHandled)
            return false;

        if (owner != null && context.Boss != null && !ReferenceEquals(owner, context.Boss))
            return false;

        if (owner != null && context.Boss == null && legacyDrop != null && context.LegacyBossDrop != legacyDrop)
            return false;

        return true;
    }

    private BossDrop ResolveLegacyDrop(BossRewardContext context)
    {
        if (legacyDrop != null)
            return legacyDrop;

        if (context != null && context.LegacyBossDrop != null)
            return context.LegacyBossDrop;

        return context != null && context.Boss != null ? context.Boss.GetComponent<BossDrop>() : null;
    }

    private Transform ResolveScatterOrigin(BossRewardContext context, BossDrop referenceDrop)
    {
        if (context != null && context.Boss != null)
            return context.Boss.transform;

        if (referenceDrop != null)
            return referenceDrop.transform;

        return transform;
    }

    private static bool SpawnRewardsCore(
        BossRewardContext context,
        GameObject resolvedChestPrefab,
        Transform resolvedChestSpawnPoint,
        GameObject resolvedMagicStonePrefab,
        Transform scatterOrigin,
        float resolvedScatterRadius,
        IReadOnlyList<BossSpecificLoot> legacyBonusLoots,
        Object logContext)
    {
        bool spawnedAny = false;
        spawnedAny |= TryRunRewardStep(
            () => SpawnTreasureChest(context, resolvedChestPrefab, resolvedChestSpawnPoint, legacyBonusLoots),
            "SpawnTreasureChest",
            logContext);
        spawnedAny |= TryRunRewardStep(
            () => SpawnBossCurrency(context, resolvedMagicStonePrefab, scatterOrigin, resolvedScatterRadius),
            "SpawnBossCurrency",
            logContext);
        spawnedAny |= TryRunRewardStep(
            () => SpawnBossFieldHeals(context, scatterOrigin, resolvedScatterRadius),
            "SpawnBossFieldHeals",
            logContext);

        return spawnedAny;
    }

    private static bool SpawnTreasureChest(
        BossRewardContext context,
        GameObject resolvedChestPrefab,
        Transform resolvedChestSpawnPoint,
        IReadOnlyList<BossSpecificLoot> legacyBonusLoots)
    {
        if (resolvedChestPrefab == null)
            return false;

        Vector3 spawnPosition = ResolveChestSpawnPosition(context, resolvedChestSpawnPoint);
        GameObject chestObject = Object.Instantiate(resolvedChestPrefab, spawnPosition, Quaternion.identity);
        TreasureChest chest = chestObject.GetComponent<TreasureChest>();
        if (chest == null)
            return true;

        var finalLoots = new List<ScriptableObject>();
        BossRewardModifierAggregate modifiers = context != null ? context.RewardModifiers : default;

        if (LootManager.Instance != null)
        {
            List<ScriptableObject> baseLoots = LootManager.Instance.GenerateChestLoot(modifiers.ChestModifierDelta);
            if (baseLoots != null)
                finalLoots.AddRange(baseLoots);
        }

        AddRolledBonusLoots(finalLoots, legacyBonusLoots);
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
        BossRewardModifierAggregate modifiers = context != null ? context.RewardModifiers : default;
        int count = LootManager.Instance != null ? LootManager.Instance.GetBossMagicStoneCount() : 0;
        count += modifiers.MagicStoneBonus;
        if (count <= 0 || resolvedMagicStonePrefab == null)
            return false;

        Vector3 origin = ResolveScatterOriginPosition(context, scatterOrigin);
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
        BossRewardModifierAggregate modifiers = context != null ? context.RewardModifiers : default;
        int count = Mathf.Max(0, modifiers.FieldHealPickupBonus);
        if (count <= 0 || LootManager.Instance == null)
            return false;

        Vector3 origin = ResolveScatterOriginPosition(context, scatterOrigin);
        float radius = Mathf.Max(0f, resolvedScatterRadius);
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = origin + (Vector3)(Random.insideUnitCircle * radius);
            LootManager.Instance.SpawnFieldHealPickup(spawnPosition);
        }

        return true;
    }

    private static Vector3 ResolveChestSpawnPosition(BossRewardContext context, Transform resolvedChestSpawnPoint)
    {
        if (resolvedChestSpawnPoint != null)
            return resolvedChestSpawnPoint.position;

        if (context != null && context.Boss != null)
            return context.Boss.transform.position;

        if (context != null && context.LegacyBossDrop != null)
            return context.LegacyBossDrop.transform.position;

        return Vector3.zero;
    }

    private static Vector3 ResolveScatterOriginPosition(BossRewardContext context, Transform scatterOrigin)
    {
        if (scatterOrigin != null)
            return scatterOrigin.position;

        if (context != null && context.Boss != null)
            return context.Boss.transform.position;

        if (context != null && context.LegacyBossDrop != null)
            return context.LegacyBossDrop.transform.position;

        return Vector3.zero;
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
