using System;

internal sealed class UpgradePurchaseCompletionService
{
    private readonly Func<PlayerInteractor2D> resolveCurrentPlayer;
    private readonly UpgradeEffectApplier effectApplier;
    private readonly Action<int, PlayerInteractor2D> markNodeAppliedForCurrentPlayer;
    private readonly Action<PlayerInteractor2D> applyHubTargetStates;
    private readonly Action<UpgradeCinematicRequest> enqueueCinematic;
    private readonly Action checkAndUnlockNodesAfterPurchase;
    private readonly UpgradeNotificationService notifications;

    public UpgradePurchaseCompletionService(
        Func<PlayerInteractor2D> resolveCurrentPlayer,
        UpgradeEffectApplier effectApplier,
        Action<int, PlayerInteractor2D> markNodeAppliedForCurrentPlayer,
        Action<PlayerInteractor2D> applyHubTargetStates,
        Action<UpgradeCinematicRequest> enqueueCinematic,
        Action checkAndUnlockNodesAfterPurchase,
        UpgradeNotificationService notifications)
    {
        this.resolveCurrentPlayer = resolveCurrentPlayer;
        this.effectApplier = effectApplier;
        this.markNodeAppliedForCurrentPlayer = markNodeAppliedForCurrentPlayer;
        this.applyHubTargetStates = applyHubTargetStates;
        this.enqueueCinematic = enqueueCinematic;
        this.checkAndUnlockNodesAfterPurchase = checkAndUnlockNodesAfterPurchase;
        this.notifications = notifications;
    }

    public void Complete(UpgradeNodeSO node)
    {
        PlayerInteractor2D player = resolveCurrentPlayer();
        effectApplier.ApplyUpgrade(node, player);
        markNodeAppliedForCurrentPlayer(node.nodeID, player);
        applyHubTargetStates(player);
        QueueUpgradeCinematics(node);
        RunModifierService.Instance?.RebuildFromPurchasedUpgrades();

        if (RewardDisplayService.Instance != null)
            RewardDisplayService.Instance.ShowUpgradeReward(node);

        checkAndUnlockNodesAfterPurchase();
        notifications?.RequestImmediateSave();
        notifications?.NotifyDataChanged();
    }

    private void QueueUpgradeCinematics(UpgradeNodeSO node)
    {
        if (IsShopActivationCinematicUpgrade(node))
            enqueueCinematic(new UpgradeCinematicRequest(UpgradeCinematicType.ShopActivated, node.nodeID));
    }

    private static bool IsShopActivationCinematicUpgrade(UpgradeNodeSO node)
    {
        if (node?.effects == null)
            return false;

        for (int i = 0; i < node.effects.Count; i++)
        {
            if (node.effects[i] is ShopRunModifierUpgradeEffect shopEffect &&
                shopEffect.Delta.shopEnabled)
            {
                return true;
            }
        }

        return false;
    }
}
