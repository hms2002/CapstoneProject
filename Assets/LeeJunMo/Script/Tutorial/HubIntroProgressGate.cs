public static class HubIntroProgressGate
{
    public const string DefaultDarkLordTutorialCompletionId = "darklord_tutorial_forced_defeat_completed";
    public const string DefaultHubIntroSeenId = "hub_intro_after_darklord_seen";

    public static bool ShouldPlayAfterDarkLordTutorial(
        string darkLordTutorialCompletionId = DefaultDarkLordTutorialCompletionId,
        string hubIntroSeenId = DefaultHubIntroSeenId,
        bool allowEditorBypassTutorialCompletion = false)
    {
        if (TutorialProgressStore.IsCompleted(ResolveSeenId(hubIntroSeenId)))
            return false;

        if (TutorialProgressStore.IsCompleted(ResolveCompletionId(darkLordTutorialCompletionId)))
            return true;

        return IsEditorBypassAllowed(allowEditorBypassTutorialCompletion);
    }

    public static bool MarkDarkLordTutorialForcedDefeatCompleted(
        string darkLordTutorialCompletionId = DefaultDarkLordTutorialCompletionId,
        bool saveImmediately = true)
    {
        return TutorialProgressStore.MarkCompleted(ResolveCompletionId(darkLordTutorialCompletionId), saveImmediately);
    }

    public static bool MarkHubIntroSeen(
        string hubIntroSeenId = DefaultHubIntroSeenId,
        bool saveImmediately = true)
    {
        return TutorialProgressStore.MarkCompleted(ResolveSeenId(hubIntroSeenId), saveImmediately);
    }

    public static string ResolveCompletionId(string darkLordTutorialCompletionId)
    {
        return ResolveId(darkLordTutorialCompletionId, DefaultDarkLordTutorialCompletionId);
    }

    public static string ResolveSeenId(string hubIntroSeenId)
    {
        return ResolveId(hubIntroSeenId, DefaultHubIntroSeenId);
    }

    private static string ResolveId(string id, string fallback)
    {
        return string.IsNullOrWhiteSpace(id) ? fallback : id.Trim();
    }

    private static bool IsEditorBypassAllowed(bool allowEditorBypassTutorialCompletion)
    {
#if UNITY_EDITOR
        return allowEditorBypassTutorialCompletion;
#else
        return false;
#endif
    }
}
