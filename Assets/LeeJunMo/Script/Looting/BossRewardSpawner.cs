using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossRewardSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private BossBattleEndPrefabCatalogSO prefabCatalog;

    [Header("Scatter")]
    [SerializeField, Min(0f)] private float scatterRadius = 1.5f;

    private BossControllerBase owner;
    private bool hasSpawned;

    private void Awake()
    {
        owner = GetComponentInParent<BossControllerBase>();
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

        BossBattleEndAnchors anchors = BossBattleEndAnchors.Resolve(context, this);

        BossRewardSpawnResult result = BossRewardSpawnService.Spawn(new BossRewardSpawnRequest(
            context,
            context.SpecialRewardPreset,
            prefabCatalog != null ? prefabCatalog.TreasureChestPrefab : null,
            ResolveChestSpawnPoint(anchors),
            prefabCatalog != null ? prefabCatalog.MagicStonePrefab : null,
            ResolveScatterOrigin(anchors),
            scatterRadius,
            this));

        if (!result.SpawnedAny)
            return;

        hasSpawned = true;
        context.MarkRewardsHandled();
    }

    private bool CanHandle(BossRewardContext context)
    {
        if (hasSpawned || context == null || context.RewardsHandled)
            return false;

        if (owner != null && context.Boss != null && !ReferenceEquals(owner, context.Boss))
            return false;

        return true;
    }

    private Transform ResolveChestSpawnPoint(BossBattleEndAnchors anchors)
    {
        return anchors != null ? anchors.RewardSpawnPoint : null;
    }

    private Transform ResolveScatterOrigin(BossBattleEndAnchors anchors)
    {
        if (anchors != null && anchors.ScatterOrigin != null)
            return anchors.ScatterOrigin;

        return anchors != null ? anchors.RewardSpawnPoint : null;
    }

}
