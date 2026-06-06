public readonly struct RunRewardModifierSnapshot
{
    public static RunRewardModifierSnapshot Empty => new RunRewardModifierSnapshot(
        default,
        default,
        default,
        default);

    public GraveRunModifierDelta GraveModifiers { get; }
    public ChestRunModifierDelta ChestModifiers { get; }
    public ShopRunModifierDelta ShopModifiers { get; }
    public BossRewardModifierAggregate BossRewardModifiers { get; }
    public BossRunModifierDelta BossModifiers => BossRewardModifiers.ToBossRunModifierDelta();

    public RunRewardModifierSnapshot(
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
