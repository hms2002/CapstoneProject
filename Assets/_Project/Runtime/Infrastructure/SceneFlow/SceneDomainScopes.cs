using System;
using CapstoneAudio;
using UnityEngine;

// 책임: 씬 도메인 전환 전역 서비스 인스턴스를 보장한다.
internal static class SceneDomainAppScopeServices
{
    public static void Ensure()
    {
        SceneTransitionCoordinator.EnsureInstance();
        SceneFadeTransitionService.EnsureInstance(allowRuntimeFallback: true);
        LoadingOverlayController.EnsureInstance();
        PresentationPreloadService.EnsureInstance();
        PortalRouteManager.EnsureInstance();
        GamePlayDataManager.EnsureInstance();
        MouseCursorService.EnsureInstance();
    }
}

// 책임: 게임플레이 씬 진입 시 카메라, BGM, UI 프레젠테이션 세션을 준비한다.
internal static class SceneDomainGameplaySessionScope
{
    public static void Ensure(SceneDomainSceneInfo sceneInfo)
    {
        if (!sceneInfo.IsGameplayScene)
            return;

        CameraBootstrap.EnsureRuntimeRigForCurrentScene();
        RunRouteBgmService.EnsureInstance();
        AffectionPresentationPlayback.PrepareSceneInstance();
        ChoiceFailurePresentationPlayback.PrepareSceneInstance();
    }
}

// 책임: 타이틀 씬 복귀 시 DontDestroyOnLoad 영역의 게임플레이 잔여 객체를 정리한다.
internal static class SceneDomainTitleCleanupScope
{
    private const string DontDestroyOnLoadSceneName = "DontDestroyOnLoad";

    public static void Cleanup()
    {
        SoundManager.Instance?.StopMusic();
        RunRouteBgmService.EnsureInstance()?.ForceRefreshActiveSceneBgm();
        LoadingOverlayController.Instance?.ForceHidePresentation();
        PortalRouteManager.Instance?.ClearPlan();

        DestroyPersistentCleanupTargets();
        DestroyPersistentOfType<CameraBootstrap>();
    }

    private static void DestroyPersistentCleanupTargets()
    {
        MonoBehaviour[] instances = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < instances.Length; i++)
        {
            MonoBehaviour instance = instances[i];
            if (instance == null || instance is not ITitleScenePersistentCleanupTarget)
                continue;

            if (!string.Equals(instance.gameObject.scene.name, DontDestroyOnLoadSceneName, StringComparison.Ordinal))
                continue;

            UnityEngine.Object.Destroy(instance.gameObject);
        }
    }

    private static void DestroyPersistentOfType<T>() where T : Component
    {
        T[] instances = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < instances.Length; i++)
        {
            T instance = instances[i];
            if (instance == null)
                continue;

            if (!string.Equals(instance.gameObject.scene.name, DontDestroyOnLoadSceneName, StringComparison.Ordinal))
                continue;

            UnityEngine.Object.Destroy(instance.gameObject);
        }
    }
}
