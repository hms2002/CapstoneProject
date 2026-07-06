using UnityEngine.SceneManagement;

internal enum SceneDomainLoadAction
{
    Ignore = 0,
    CleanupTitleScope = 1,
    EnsureGameplaySessionScope = 2
}

// 책임: 로드된 Scene의 이름, 유효성, 타이틀/허브 여부를 계산해 보관한다.
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

// 책임: 씬 도메인 정보와 그에 따른 bootstrap/session 처리 액션을 함께 전달한다.
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

// 책임: 로드된 씬 이름/종류에 따라 타이틀 정리 또는 게임플레이 세션 보장 정책을 결정한다.
internal static class SceneDomainScenePolicy
{
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
        return SceneDomainNamePolicy.IsTitleSceneName(sceneName);
    }

    public static bool IsHubSceneName(string sceneName)
    {
        return SceneDomainNamePolicy.IsHubSceneName(sceneName);
    }
}
