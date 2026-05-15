using UnityEngine.SceneManagement;

internal static class CameraBootstrapScenePolicy
{
    public static bool ShouldSkipRuntimeBootstrap(Scene scene)
    {
        return IsTitleScene(scene);
    }

    public static bool ShouldReleaseRuntimeRig(Scene scene)
    {
        return IsTitleScene(scene);
    }

    private static bool IsTitleScene(Scene scene)
    {
        return scene.IsValid() && SceneDomainScenePolicy.IsTitleSceneName(scene.name);
    }
}
