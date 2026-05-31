using System;
using CapstoneAudio;
using UnityEngine;

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

internal static class SceneDomainGameplaySessionScope
{
    public static void Ensure(SceneDomainSceneInfo sceneInfo)
    {
        if (!sceneInfo.IsGameplayScene)
            return;

        CameraBootstrap.EnsureRuntimeRigForCurrentScene();
        CameraShakeService.EnsureInstance();
        RunRouteBgmService.EnsureInstance();
        AffectionGainScreenEffect.PrepareSceneInstance();
        ChoiceFailureScreenEffect.PrepareSceneInstance();
    }
}

internal static class SceneDomainTitleCleanupScope
{
    private const string DontDestroyOnLoadSceneName = "DontDestroyOnLoad";

    public static void Cleanup()
    {
        SoundManager.Instance?.StopMusic();
        RunRouteBgmService.EnsureInstance()?.ForceRefreshActiveSceneBgm();
        LoadingOverlayController.Instance?.ForceHidePresentation();
        PortalRouteManager.Instance?.ClearPlan();

        DestroyPersistentOfType<PauseMenuUI>();
        DestroyPersistentOfType<SettingsPanelUI>();
        DestroyPersistentOfType<KeyBindingPanelUI>();
        DestroyPersistentOfType<UIManager>();
        DestroyPersistentOfType<GlobalUIRoot>();
        DestroyPersistentOfType<CameraBootstrap>();
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
