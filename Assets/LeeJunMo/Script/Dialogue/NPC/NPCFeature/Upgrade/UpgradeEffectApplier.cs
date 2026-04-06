using System.Collections.Generic;

public sealed class UpgradeEffectApplier
{
    public void ApplyUpgrade(UpgradeNodeSO node, PlayerInteractor2D player)
    {
        if (node == null)
            return;

        node.ApplyOnPurchase(player);
    }

    public void ReapplyPurchasedEffects(IEnumerable<int> purchasedIDs, UpgradeProgressService progressService, PlayerInteractor2D player)
    {
        if (purchasedIDs == null || progressService == null || player == null)
            return;

        foreach (int id in purchasedIDs)
        {
            UpgradeNodeSO node = progressService.GetUpgradeByID(id);
            if (node != null)
                node.ReapplyPlayerEffects(player);
        }
    }
}
