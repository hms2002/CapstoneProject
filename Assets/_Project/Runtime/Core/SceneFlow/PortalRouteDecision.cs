public readonly struct PortalRouteDecision
{
    public readonly string TargetSceneName;
    public readonly string EntryPointId;
    public readonly TransitionType TransitionType;

    public bool IsValid => !string.IsNullOrWhiteSpace(TargetSceneName);

    public PortalRouteDecision(
        string targetSceneName,
        string entryPointId,
        TransitionType transitionType)
    {
        TargetSceneName = targetSceneName;
        EntryPointId = entryPointId;
        TransitionType = transitionType;
    }
}
