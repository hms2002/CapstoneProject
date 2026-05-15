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

        BossRewardSpawnResult result = BossRewardSpawnService.Spawn(new BossRewardSpawnRequest(
            context,
            resolvedChestPrefab,
            resolvedChestSpawnPoint,
            resolvedMagicStonePrefab,
            scatterOrigin,
            scatterRadius,
            referenceDrop != null ? referenceDrop.bossUniqueLoots : null,
            this));

        if (!result.SpawnedAny)
            return;

        hasSpawned = true;
        context.MarkRewardsHandled();
    }

    public static bool SpawnFromLegacyDrop(BossDrop legacyDrop, BossRewardContext context)
    {
        if (legacyDrop == null)
            return false;

        BossRewardSpawnResult result = BossRewardSpawnService.Spawn(new BossRewardSpawnRequest(
            context,
            legacyDrop.chestPrefab,
            legacyDrop.chestSpawnPoint,
            legacyDrop.magicStonePrefab,
            legacyDrop.transform,
            1.5f,
            legacyDrop.bossUniqueLoots,
            legacyDrop));
        return result.SpawnedAny;
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

}
