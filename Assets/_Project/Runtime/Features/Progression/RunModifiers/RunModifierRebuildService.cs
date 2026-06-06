internal readonly struct RunModifierRebuildRequest
{
    public UpgradeSaveData UpgradeSaveData { get; }
    public UpgradeNodeSO[] CachedUpgradeNodes { get; }
    public UpgradeManager UpgradeManager { get; }
    public NPCManager NpcManager { get; }
    public GameData GameData { get; }
    public GamePlayData RunData { get; }
    public string UpgradeNodeResourcePath { get; }

    public RunModifierRebuildRequest(
        UpgradeSaveData upgradeSaveData,
        UpgradeNodeSO[] cachedUpgradeNodes,
        UpgradeManager upgradeManager,
        NPCManager npcManager,
        GameData gameData,
        GamePlayData runData,
        string upgradeNodeResourcePath)
    {
        UpgradeSaveData = upgradeSaveData;
        CachedUpgradeNodes = cachedUpgradeNodes;
        UpgradeManager = upgradeManager;
        NpcManager = npcManager;
        GameData = gameData;
        RunData = runData;
        UpgradeNodeResourcePath = upgradeNodeResourcePath;
    }
}

internal readonly struct RunModifierRebuildResult
{
    public UpgradeNodeSO[] CachedUpgradeNodes { get; }
    public GraveRunModifierDelta GraveModifiers { get; }
    public ChestRunModifierDelta ChestModifiers { get; }
    public ShopRunModifierDelta ShopModifiers { get; }
    public BossRewardModifierAggregate BossRewardModifiers { get; }

    public RunModifierRebuildResult(
        UpgradeNodeSO[] cachedUpgradeNodes,
        GraveRunModifierDelta graveModifiers,
        ChestRunModifierDelta chestModifiers,
        ShopRunModifierDelta shopModifiers,
        BossRewardModifierAggregate bossRewardModifiers)
    {
        CachedUpgradeNodes = cachedUpgradeNodes;
        GraveModifiers = graveModifiers;
        ChestModifiers = chestModifiers;
        ShopModifiers = shopModifiers;
        BossRewardModifiers = bossRewardModifiers;
    }
}

internal static class RunModifierRebuildService
{
    public static RunModifierRebuildResult Rebuild(RunModifierRebuildRequest request)
    {
        UpgradeNodeSO[] upgradeNodes = ResolveUpgradeNodes(request);
        RunModifierAggregationResult aggregationResult = RunModifierAggregationService.Aggregate(
            new RunModifierAggregationRequest(
                request.UpgradeSaveData,
                upgradeNodes,
                request.NpcManager,
                request.GameData,
                request.RunData));

        return new RunModifierRebuildResult(
            upgradeNodes,
            aggregationResult.GraveModifiers,
            aggregationResult.ChestModifiers,
            aggregationResult.ShopModifiers,
            aggregationResult.BossRewardModifiers);
    }

    private static UpgradeNodeSO[] ResolveUpgradeNodes(RunModifierRebuildRequest request)
    {
        if (!ShouldLoadUpgradeNodes(request.UpgradeSaveData))
            return request.CachedUpgradeNodes;

        RunModifierUpgradeNodeLoadResult result = RunModifierUpgradeNodeProvider.Load(
            new RunModifierUpgradeNodeLoadRequest(
                request.CachedUpgradeNodes,
                request.UpgradeManager,
                request.UpgradeNodeResourcePath));

        return result.Nodes;
    }

    private static bool ShouldLoadUpgradeNodes(UpgradeSaveData saveData)
    {
        return saveData?.purchasedIDs != null && saveData.purchasedIDs.Count > 0;
    }
}
