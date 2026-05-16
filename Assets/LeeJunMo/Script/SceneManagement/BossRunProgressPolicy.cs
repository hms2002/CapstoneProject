using System.Runtime.CompilerServices;

internal readonly struct BossRunProgressRequest
{
    public BossControllerBase Boss { get; }
    public PortalRouteManager RouteManager { get; }
    public BossRewardModifierAggregate RewardModifiers { get; }

    public BossRunProgressRequest(
        BossControllerBase boss,
        PortalRouteManager routeManager,
        BossRewardModifierAggregate rewardModifiers)
    {
        Boss = boss;
        RouteManager = routeManager;
        RewardModifiers = rewardModifiers;
    }
}

internal readonly struct BossRunProgressResult
{
    public BossControllerBase Boss { get; }
    public CorridorBossRouteSetSO RouteSet { get; }
    public int RouteSetKey { get; }
    public bool IsFinalRouteSet { get; }
    public int BossIdentityKey { get; }
    public BossRewardModifierAggregate RewardModifiers { get; }

    public BossRunProgressResult(
        BossControllerBase boss,
        CorridorBossRouteSetSO routeSet,
        int routeSetKey,
        bool isFinalRouteSet,
        int bossIdentityKey,
        BossRewardModifierAggregate rewardModifiers)
    {
        Boss = boss;
        RouteSet = routeSet;
        RouteSetKey = routeSetKey;
        IsFinalRouteSet = isFinalRouteSet;
        BossIdentityKey = bossIdentityKey;
        RewardModifiers = rewardModifiers;
    }

    public BossRewardContext ToRewardContext()
    {
        return new BossRewardContext(
            Boss,
            RouteSet,
            RouteSetKey,
            IsFinalRouteSet,
            RewardModifiers);
    }
}

internal static class BossRunProgressPolicy
{
    public static BossRunProgressResult Evaluate(BossRunProgressRequest request)
    {
        PortalRouteManager routeManager = request.RouteManager;
        CorridorBossRouteSetSO routeSet = routeManager != null ? routeManager.CurrentStageSet : null;
        int routeSetKey = ResolveRouteSetKey(request.Boss, routeManager);
        bool isFinalRouteSet = IsCurrentRouteFinalBoss(routeManager, routeSet);
        int bossIdentityKey = GetObjectIdentityKey(request.Boss);

        return new BossRunProgressResult(
            request.Boss,
            routeSet,
            routeSetKey,
            isFinalRouteSet,
            bossIdentityKey,
            request.RewardModifiers);
    }

    public static bool ShouldTrackFinalBossCombat(BossRunProgressResult result)
    {
        return result.Boss != null && result.IsFinalRouteSet;
    }

    public static int GetObjectIdentityKey(UnityEngine.Object unityObject)
    {
        return unityObject != null ? RuntimeHelpers.GetHashCode(unityObject) : 0;
    }

    private static bool IsCurrentRouteFinalBoss(
        PortalRouteManager routeManager,
        CorridorBossRouteSetSO currentStage)
    {
        if (routeManager == null || !routeManager.HasActivePlan)
            return false;

        RunRouteCatalogSO activeCatalog = routeManager.ActiveRouteCatalog;
        if (activeCatalog != null && currentStage != null)
            return ReferenceEquals(activeCatalog.FinalRouteSet, currentStage);

        return routeManager.TotalStageCount > 0 &&
               routeManager.CurrentStageIndex == routeManager.TotalStageCount - 1;
    }

    private static int ResolveRouteSetKey(
        BossControllerBase boss,
        PortalRouteManager routeManager)
    {
        if (routeManager != null && routeManager.HasActivePlan)
        {
            CorridorBossRouteSetSO routeSet = routeManager.CurrentStageSet;
            if (routeSet != null)
                return GetObjectIdentityKey(routeSet);

            return routeManager.CurrentStageIndex + 1;
        }

        if (boss != null)
            return GetObjectIdentityKey(boss);

        return 0;
    }
}
