using System.Collections.Generic;

internal readonly struct RunModifierAggregationRequest
{
    public UpgradeSaveData UpgradeSaveData { get; }
    public UpgradeNodeSO[] UpgradeNodes { get; }
    public NPCManager NpcManager { get; }
    public GameData GameData { get; }
    public GamePlayData RunData { get; }

    public RunModifierAggregationRequest(
        UpgradeSaveData upgradeSaveData,
        UpgradeNodeSO[] upgradeNodes,
        NPCManager npcManager,
        GameData gameData,
        GamePlayData runData)
    {
        UpgradeSaveData = upgradeSaveData;
        UpgradeNodes = upgradeNodes;
        NpcManager = npcManager;
        GameData = gameData;
        RunData = runData;
    }
}

internal readonly struct RunModifierAggregationResult
{
    public static RunModifierAggregationResult Empty => new RunModifierAggregationResult(
        default,
        default,
        default,
        default);

    public GraveRunModifierDelta GraveModifiers { get; }
    public ChestRunModifierDelta ChestModifiers { get; }
    public ShopRunModifierDelta ShopModifiers { get; }
    public BossRewardModifierAggregate BossRewardModifiers { get; }

    public RunModifierAggregationResult(
        GraveRunModifierDelta graveModifiers,
        ChestRunModifierDelta chestModifiers,
        ShopRunModifierDelta shopModifiers,
        BossRewardModifierAggregate bossRewardModifiers)
    {
        GraveModifiers = graveModifiers;
        ChestModifiers = chestModifiers;
        ShopModifiers = shopModifiers;
        BossRewardModifiers = bossRewardModifiers;
    }
}

internal static class RunModifierAggregationService
{
    public static RunModifierAggregationResult Aggregate(RunModifierAggregationRequest request)
    {
        GraveRunModifierDelta graveModifiers = default;
        ChestRunModifierDelta chestModifiers = default;
        ShopRunModifierDelta shopModifiers = default;
        BossRewardModifierAggregate bossRewardModifiers = default;

        ApplyPurchasedUpgradeModifiers(
            request.UpgradeSaveData,
            request.UpgradeNodes,
            ref graveModifiers,
            ref chestModifiers,
            ref shopModifiers);

        ApplyAffectionModifiers(
            request.NpcManager,
            request.GameData,
            request.RunData,
            ref bossRewardModifiers);

        return new RunModifierAggregationResult(
            graveModifiers,
            chestModifiers,
            shopModifiers,
            bossRewardModifiers);
    }

    private static void ApplyPurchasedUpgradeModifiers(
        UpgradeSaveData saveData,
        UpgradeNodeSO[] nodes,
        ref GraveRunModifierDelta graveModifiers,
        ref ChestRunModifierDelta chestModifiers,
        ref ShopRunModifierDelta shopModifiers)
    {
        if (saveData?.purchasedIDs == null || saveData.purchasedIDs.Count == 0)
            return;

        if (nodes == null || nodes.Length == 0)
            return;

        foreach (int purchasedId in saveData.purchasedIDs)
        {
            UpgradeNodeSO node = FindNodeById(nodes, purchasedId);
            if (node == null || node.effects == null)
                continue;

            foreach (UpgradeEffectSO effect in node.effects)
            {
                ApplyUpgradeModifier(
                    effect,
                    ref graveModifiers,
                    ref chestModifiers,
                    ref shopModifiers);
            }
        }
    }

    private static void ApplyUpgradeModifier(
        UpgradeEffectSO effect,
        ref GraveRunModifierDelta graveModifiers,
        ref ChestRunModifierDelta chestModifiers,
        ref ShopRunModifierDelta shopModifiers)
    {
        if (effect is GraveRunModifierUpgradeEffect graveEffect)
        {
            graveModifiers.Add(graveEffect.Delta);
            return;
        }

        if (effect is ChestRunModifierUpgradeEffect chestEffect)
        {
            chestModifiers.Add(chestEffect.Delta);
            return;
        }

        if (effect is ShopRunModifierUpgradeEffect shopEffect)
        {
            shopModifiers.Add(shopEffect.Delta);
        }
    }

    private static void ApplyAffectionModifiers(
        NPCManager npcManager,
        GameData gameData,
        GamePlayData runData,
        ref BossRewardModifierAggregate bossRewardModifiers)
    {
        if (npcManager == null)
            return;

        Dictionary<int, int> affectionAmounts = BuildAffectionAmountMap(gameData, runData);
        foreach (KeyValuePair<int, int> entry in affectionAmounts)
        {
            NPCData npcData = npcManager.GetNPCData(entry.Key);
            if (npcData?.affectionRewards == null)
                continue;

            foreach (AffectionReward reward in npcData.affectionRewards)
            {
                if (reward.effect == null || reward.targetLevel > entry.Value)
                    continue;

                if (reward.effect is BossAffectionRunModifierEffect bossEffect)
                    bossRewardModifiers.Add(bossEffect.ModifierAggregate);
            }
        }
    }

    private static Dictionary<int, int> BuildAffectionAmountMap(GameData gameData, GamePlayData runData)
    {
        var amounts = new Dictionary<int, int>();

        if (gameData?.affectionData?.affectionRecords != null)
        {
            foreach (AffectionRecord record in gameData.affectionData.affectionRecords)
            {
                if (record != null)
                    amounts[record.npcId] = record.amount;
            }
        }

        if (runData?.pendingRunAffectionChanges != null)
        {
            foreach (PendingRunAffectionChange change in runData.pendingRunAffectionChanges)
            {
                if (change == null)
                    continue;

                amounts.TryGetValue(change.npcId, out int currentAmount);
                amounts[change.npcId] = currentAmount + change.delta;
            }
        }

        return amounts;
    }

    private static UpgradeNodeSO FindNodeById(UpgradeNodeSO[] nodes, int nodeId)
    {
        if (nodes == null)
            return null;

        for (int i = 0; i < nodes.Length; i++)
        {
            UpgradeNodeSO node = nodes[i];
            if (node != null && node.nodeID == nodeId)
                return node;
        }

        return null;
    }
}
