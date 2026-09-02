using UnityEngine;

/// <summary>
/// 책임: 에디터 직접 씬 시작 보정에 필요한 런타임 안전 개발용 기본값과 판정을 제공한다.
/// </summary>
internal static class SceneDomainDevelopmentStartPolicy
{
    public const int DevelopmentDefaultSlotIndex = 0;
    public const string RunTimeLimitConfigAssetPath =
        "Assets/_Project/Data/Progression/RunTimer/RunTimeLimitConfig.asset";
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

#if UNITY_EDITOR
/// <summary>
/// 책임:
/// - Hub를 거치지 않은 에디터 직접 게임플레이 시작에 기존 run timer 패키지를 제공한다.
/// - 자신이 생성한 fallback만 추적하고 Title 복귀 때 정리한다.
/// </summary>
internal static class SceneDomainDevelopmentRunTimerPolicy
{
    private static RunTimeLimitSystem ownedTimer;

    public static void ResetOwnership()
    {
        ownedTimer = null;
    }

    public static void EnsureBeforeRunStart()
    {
        if (RunTimeLimitSystem.Instance != null)
            return;

        RunTimeLimitSystem existing = Object.FindAnyObjectByType<RunTimeLimitSystem>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Debug.LogWarning(
                "[SceneDomainDevelopmentRunTimerPolicy] A RunTimeLimitSystem exists but is not the active " +
                "instance. Editor direct-start timer creation was skipped.",
                existing);
            return;
        }

        RunTimeLimitConfig config = EditorAuthoringPlayback.LoadAssetAtPath<RunTimeLimitConfig>(
            SceneDomainDevelopmentStartPolicy.RunTimeLimitConfigAssetPath);
        if (config == null)
        {
            Debug.LogWarning(
                "[SceneDomainDevelopmentRunTimerPolicy] RunTimeLimitConfig is missing at " +
                $"'{SceneDomainDevelopmentStartPolicy.RunTimeLimitConfigAssetPath}'.");
            return;
        }

        GameObject timerRoot = new GameObject("RunTimeLimitSystemManager [Editor Direct Start]");
        DefaultStageTimerPolicy stagePolicy = timerRoot.AddComponent<DefaultStageTimerPolicy>();
        RunTimeLimitSystem timer = timerRoot.AddComponent<RunTimeLimitSystem>();
        timer.ConfigureEditorDevelopmentStart(config, stagePolicy);
        timerRoot.AddComponent<RunTimeOverReturnToHub>();
        ownedTimer = timer;
    }

    public static void CleanupOwnedTimer()
    {
        if (ownedTimer == null)
            return;

        Object.Destroy(ownedTimer.gameObject);
        ownedTimer = null;
    }
}
#endif
