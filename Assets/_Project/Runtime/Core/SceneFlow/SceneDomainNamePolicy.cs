using System;

/// <summary>
/// 책임 : 계층과 무관하게 공유되는 핵심 씬 이름 분류 규칙을 제공한다.
/// </summary>
public static class SceneDomainNamePolicy
{
    private const string TitleSceneName = "TitleScene";
    private const string HubSceneName = "ProtoTypeHub";

    public static bool IsTitleSceneName(string sceneName)
    {
        return string.Equals(sceneName, TitleSceneName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHubSceneName(string sceneName)
    {
        return string.Equals(sceneName, HubSceneName, StringComparison.OrdinalIgnoreCase);
    }
}
