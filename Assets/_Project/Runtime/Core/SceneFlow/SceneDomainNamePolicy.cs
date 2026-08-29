using System;
using UnityEngine;
using UnityEngine.SceneManagement;

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

/// <summary>
/// 책임: Editor에서 첫 gameplay scene을 직접 Play한 경우에만 적용할 개발용 시작 문맥을 공유한다.
/// Player 빌드에서는 항상 비활성인 no-op 문맥이다.
/// </summary>
public static class EditorDirectSceneStartContext
{
#if UNITY_EDITOR
    private static int directHubStartSceneHandle = -1;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        Clear();
    }

    public static bool IsDirectHubStart(Scene ownerScene)
    {
#if UNITY_EDITOR
        return ownerScene.IsValid() && ownerScene.handle == directHubStartSceneHandle;
#else
        return false;
#endif
    }

    /// <summary>
    /// SceneDomainCoordinator만 최초 direct Hub 판정 직후 호출한다.
    /// </summary>
    public static void MarkDirectHubStart(Scene scene)
    {
#if UNITY_EDITOR
        directHubStartSceneHandle = scene.IsValid() ? scene.handle : -1;
#endif
    }

    public static void Clear()
    {
#if UNITY_EDITOR
        directHubStartSceneHandle = -1;
#endif
    }
}
