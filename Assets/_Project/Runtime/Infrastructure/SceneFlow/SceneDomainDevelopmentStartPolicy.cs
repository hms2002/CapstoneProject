/// <summary>
/// 책임: 에디터 직접 씬 시작 보정에 필요한 런타임 안전 개발용 기본값과 판정을 제공한다.
/// </summary>
internal static class SceneDomainDevelopmentStartPolicy
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
