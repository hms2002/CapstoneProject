#if UNITY_EDITOR
internal static class SceneDomainEditorDirectStartPolicy
{
    public const int DevelopmentDefaultSlotIndex = 0;
    public const string BlockControlByUiTagSetResourcePath = "Tags/TagSet/TS_BlockControlByUI";
    public const string InteractBlockedTagResourcePath = "Tags/State.Interact.Blocked";

    public static bool IsDirectGameplayStart(SceneDomainSceneInfo sceneInfo)
    {
        return sceneInfo.IsGameplayScene;
    }

    public static bool ShouldRunPostSceneBootstrap(bool directGameplayStartActive, SceneDomainSceneInfo sceneInfo)
    {
        return directGameplayStartActive && sceneInfo.IsGameplayScene;
    }
}
#endif
