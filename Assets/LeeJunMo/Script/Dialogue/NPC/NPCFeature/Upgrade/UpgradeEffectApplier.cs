using System.Collections.Generic;

public sealed class UpgradeEffectApplier
{
    public void ApplyUpgrade(UpgradeNodeSO node, SampleTopDownPlayer player)
    {
        if (node == null || player == null)
            return;

        node.ApplyEffect(player);
    }

    public void ReapplyPurchasedEffects(IEnumerable<int> purchasedIDs, UpgradeProgressService progressService, SampleTopDownPlayer player)
    {
        if (purchasedIDs == null || progressService == null || player == null)
            return;

        foreach (int id in purchasedIDs)
        {
            UpgradeNodeSO node = progressService.GetUpgradeByID(id);
            if (node != null)
                node.ApplyEffect(player);
        }
    }
}
