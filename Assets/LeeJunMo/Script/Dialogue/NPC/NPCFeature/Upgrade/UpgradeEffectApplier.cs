using System.Collections.Generic;

public sealed class UpgradeEffectApplier
{
    public void ApplyUpgrade(UpgradeNodeSO node, PlayerInteractor2D player)
    {
        if (node == null)
            return;

        node.ApplyOnPurchase(player);
    }

    public void ReapplyPurchasedEffects(
        IEnumerable<int> purchasedIDs,
        UpgradeProgressService progressService,
        PlayerInteractor2D player,
        ISet<int> appliedNodeIds = null)
    {
        if (purchasedIDs == null || progressService == null || player == null)
            return;

        foreach (int id in purchasedIDs)
        {
            if (appliedNodeIds != null && appliedNodeIds.Contains(id))
                continue;

            UpgradeNodeSO node = progressService.GetUpgradeByID(id);
            if (node == null)
                continue;

            node.ReapplyPlayerEffects(player);
            appliedNodeIds?.Add(id);
        }
    }

    public void ApplyRunStartEffects(
        IEnumerable<int> purchasedIDs,
        UpgradeProgressService progressService,
        PlayerInteractor2D player)
    {
        if (purchasedIDs == null || progressService == null || player == null)
            return;

        UpgradeRuntimeTargetAccumulator runtimeTargets = new UpgradeRuntimeTargetAccumulator();

        foreach (int id in purchasedIDs)
        {
            UpgradeNodeSO node = progressService.GetUpgradeByID(id);
            if (node?.effects == null)
                continue;

            foreach (UpgradeEffectSO effect in node.effects)
            {
                if (effect is IUpgradeRuntimeTargetEffect runtimeTargetEffect)
                {
                    runtimeTargetEffect.AccumulateRuntimeTarget(runtimeTargets);
                    continue;
                }

                if (effect is IRunStartUpgradeEffect runStartEffect)
                    runStartEffect.ApplyAtRunStart(player);
            }
        }

        runtimeTargets.Apply(player, null);
    }

    public void ApplyImmediateTargetStates(
        IEnumerable<int> purchasedIDs,
        UpgradeProgressService progressService,
        PlayerInteractor2D player)
    {
        if (purchasedIDs == null || progressService == null || player == null)
            return;

        UpgradeRuntimeTargetAccumulator runtimeTargets = new UpgradeRuntimeTargetAccumulator();

        foreach (int id in purchasedIDs)
        {
            UpgradeNodeSO node = progressService.GetUpgradeByID(id);
            if (node?.effects == null)
                continue;

            foreach (UpgradeEffectSO effect in node.effects)
            {
                if (effect is IUpgradeRuntimeTargetEffect runtimeTargetEffect)
                    runtimeTargetEffect.AccumulateRuntimeTarget(runtimeTargets);
            }
        }

        runtimeTargets.Apply(player, null);
    }
}
