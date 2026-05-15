internal readonly struct UpgradeRunStartEffectRequest
{
    public readonly bool HasAppliedForCurrentRun;
    public readonly bool IsRunActive;
    public readonly bool HasObservedSceneLoadForCurrentRun;
    public readonly bool IsActiveSceneRunContent;

    public UpgradeRunStartEffectRequest(
        bool hasAppliedForCurrentRun,
        bool isRunActive,
        bool hasObservedSceneLoadForCurrentRun,
        bool isActiveSceneRunContent)
    {
        HasAppliedForCurrentRun = hasAppliedForCurrentRun;
        IsRunActive = isRunActive;
        HasObservedSceneLoadForCurrentRun = hasObservedSceneLoadForCurrentRun;
        IsActiveSceneRunContent = isActiveSceneRunContent;
    }
}

internal readonly struct UpgradeRunStartEffectResult
{
    public readonly bool CanApply;
    public readonly UpgradeRunStartEffectSkipReason SkipReason;

    public UpgradeRunStartEffectResult(
        bool canApply,
        UpgradeRunStartEffectSkipReason skipReason)
    {
        CanApply = canApply;
        SkipReason = skipReason;
    }
}

internal enum UpgradeRunStartEffectSkipReason
{
    None,
    AlreadyApplied,
    RunInactive,
    RunContentSceneNotObserved
}

internal static class UpgradeRunStartEffectPolicy
{
    public static UpgradeRunStartEffectResult Evaluate(UpgradeRunStartEffectRequest request)
    {
        if (request.HasAppliedForCurrentRun)
            return Skip(UpgradeRunStartEffectSkipReason.AlreadyApplied);

        if (!request.IsRunActive)
            return Skip(UpgradeRunStartEffectSkipReason.RunInactive);

        if (!request.HasObservedSceneLoadForCurrentRun && !request.IsActiveSceneRunContent)
            return Skip(UpgradeRunStartEffectSkipReason.RunContentSceneNotObserved);

        return new UpgradeRunStartEffectResult(true, UpgradeRunStartEffectSkipReason.None);
    }

    private static UpgradeRunStartEffectResult Skip(UpgradeRunStartEffectSkipReason reason)
    {
        return new UpgradeRunStartEffectResult(false, reason);
    }
}
