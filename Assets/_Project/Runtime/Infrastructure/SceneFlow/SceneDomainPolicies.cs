using System;
using UnityEngine.SceneManagement;

internal enum SceneDomainLoadAction
{
    Ignore = 0,
    CleanupTitleScope = 1,
    EnsureGameplaySessionScope = 2
}

internal readonly struct SceneDomainSceneInfo
{
    public readonly Scene Scene;
    public readonly string SceneName;
    public readonly bool IsValid;
    public readonly bool IsTitleScene;
    public readonly bool IsHubScene;

    public SceneDomainSceneInfo(Scene scene)
    {
        Scene = scene;
        IsValid = scene.IsValid();
        SceneName = IsValid ? scene.name : string.Empty;
        IsTitleScene = SceneDomainScenePolicy.IsTitleSceneName(SceneName);
        IsHubScene = SceneDomainScenePolicy.IsHubSceneName(SceneName);
    }

    public bool IsGameplayScene => IsValid && !IsTitleScene;
}

internal readonly struct SceneDomainLoadDecision
{
    public readonly SceneDomainSceneInfo SceneInfo;
    public readonly SceneDomainLoadAction Action;

    public SceneDomainLoadDecision(SceneDomainSceneInfo sceneInfo, SceneDomainLoadAction action)
    {
        SceneInfo = sceneInfo;
        Action = action;
    }

    public bool ShouldProcess => SceneInfo.IsValid && Action != SceneDomainLoadAction.Ignore;
    public bool RequiresTitleCleanup => Action == SceneDomainLoadAction.CleanupTitleScope;
    public bool RequiresGameplaySessionScope => Action == SceneDomainLoadAction.EnsureGameplaySessionScope;
}

internal static class SceneDomainScenePolicy
{
    private const string TitleSceneName = "TitleScene";
    private const string HubSceneName = "ProtoTypeHub";

    public static SceneDomainLoadDecision CreateLoadDecision(Scene scene)
    {
        SceneDomainSceneInfo sceneInfo = new SceneDomainSceneInfo(scene);
        if (!sceneInfo.IsValid)
            return new SceneDomainLoadDecision(sceneInfo, SceneDomainLoadAction.Ignore);

        SceneDomainLoadAction action = sceneInfo.IsTitleScene
            ? SceneDomainLoadAction.CleanupTitleScope
            : SceneDomainLoadAction.EnsureGameplaySessionScope;
        return new SceneDomainLoadDecision(sceneInfo, action);
    }

    public static bool IsTitleSceneName(string sceneName)
    {
        return string.Equals(sceneName, TitleSceneName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHubSceneName(string sceneName)
    {
        return string.Equals(sceneName, HubSceneName, StringComparison.OrdinalIgnoreCase);
    }
}
