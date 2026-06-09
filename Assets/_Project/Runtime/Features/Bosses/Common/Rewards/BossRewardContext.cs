public sealed class BossRewardContext
{
    public BossRewardContext(
        BossControllerBase boss,
        CorridorBossRouteSetSO routeSet,
        int routeSetKey,
        bool isFinalRouteSet,
        BossRewardModifierAggregate rewardModifiers)
    {
        Boss = boss;
        RouteSet = routeSet;
        RouteSetKey = routeSetKey;
        IsFinalRouteSet = isFinalRouteSet;
        RewardModifiers = rewardModifiers;
    }

    public BossControllerBase Boss { get; }
    public CorridorBossRouteSetSO RouteSet { get; }
    public BossSpecialRewardPresetSO SpecialRewardPreset =>
        RouteSet != null ? RouteSet.BossSpecialRewardPreset : null;
    public int RouteSetKey { get; }
    public bool IsFinalRouteSet { get; }
    public BossRewardModifierAggregate RewardModifiers { get; }
    public bool RewardsHandled { get; private set; }
    public bool PortalHandled { get; private set; }

    public void MarkRewardsHandled()
    {
        RewardsHandled = true;
    }

    public void MarkPortalHandled()
    {
        PortalHandled = true;
    }
}
