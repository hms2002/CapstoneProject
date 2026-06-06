using System;
using System.Collections.Generic;

internal sealed class UpgradeRuntimeEffectService
{
    private readonly Func<PlayerInteractor2D> resolveCurrentPlayer;
    private readonly UpgradeProgressService progressService;
    private readonly UpgradeEffectApplier effectApplier;
    private readonly Func<bool> isRunActive;
    private readonly HashSet<int> appliedPlayerEffectNodeIds = new HashSet<int>();
    private PlayerInteractor2D appliedPlayer;

    public UpgradeRuntimeEffectService(
        Func<PlayerInteractor2D> resolveCurrentPlayer,
        UpgradeProgressService progressService,
        UpgradeEffectApplier effectApplier,
        Func<bool> isRunActive)
    {
        this.resolveCurrentPlayer = resolveCurrentPlayer;
        this.progressService = progressService;
        this.effectApplier = effectApplier;
        this.isRunActive = isRunActive;
    }

    public void TryReapplyAllEffects()
    {
        PlayerInteractor2D player = ResolveCurrentPlayer();
        if (player == null)
            return;

        SyncAppliedPlayer(player);
        ReapplyPurchasedEffects(player);
        TryApplyHubTargetStates(player);
    }

    public bool TryApplyRunStartEffects(UpgradeRunStartEffectRequest request)
    {
        var timingResult = UpgradeRunStartEffectPolicy.Evaluate(request);
        if (!timingResult.CanApply)
            return false;

        PlayerInteractor2D player = ResolveCurrentPlayer();
        if (player == null || GameDataManager.Instance == null || progressService == null || effectApplier == null)
            return false;

        GameData data = GameDataManager.Instance.EnsureData();
        if (data?.upgradeData?.purchasedIDs == null)
            return false;

        effectApplier.ApplyRunStartEffects(data.upgradeData.purchasedIDs, progressService, player);
        return true;
    }

    public void TryApplyHubTargetStates(PlayerInteractor2D player)
    {
        if (isRunActive != null && isRunActive())
            return;

        if (player == null)
            player = ResolveCurrentPlayer();

        if (player == null || GameDataManager.Instance == null || progressService == null || effectApplier == null)
            return;

        GameData data = GameDataManager.Instance.EnsureData();
        if (data?.upgradeData?.purchasedIDs == null)
            return;

        effectApplier.ApplyImmediateTargetStates(data.upgradeData.purchasedIDs, progressService, player);
    }

    public void MarkNodeAppliedForCurrentPlayer(int nodeId, PlayerInteractor2D player)
    {
        if (player == null)
            return;

        SyncAppliedPlayer(player);
        appliedPlayerEffectNodeIds.Add(nodeId);
    }

    public void ResetAppliedPlayerEffects()
    {
        appliedPlayer = null;
        appliedPlayerEffectNodeIds.Clear();
    }

    private void ReapplyPurchasedEffects(PlayerInteractor2D player)
    {
        if (player == null || GameDataManager.Instance == null || progressService == null || effectApplier == null)
            return;

        GameData data = GameDataManager.Instance.EnsureData();
        if (data == null)
            return;

        data.upgradeData ??= new UpgradeSaveData();
        effectApplier.ReapplyPurchasedEffects(
            data.upgradeData.purchasedIDs,
            progressService,
            player,
            appliedPlayerEffectNodeIds);
    }

    private PlayerInteractor2D ResolveCurrentPlayer()
    {
        return resolveCurrentPlayer != null ? resolveCurrentPlayer() : null;
    }

    private void SyncAppliedPlayer(PlayerInteractor2D player)
    {
        if (appliedPlayer == player)
            return;

        appliedPlayer = player;
        appliedPlayerEffectNodeIds.Clear();
    }
}
