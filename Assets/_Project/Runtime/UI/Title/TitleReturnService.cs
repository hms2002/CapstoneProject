using System.IO;
using UnityEngine.SceneManagement;

internal readonly struct TitleReturnRequest
{
    public readonly UIManager UiManager;
    public readonly string TargetSceneName;
    public readonly GamePlayDataManager GamePlayDataManager;

    public TitleReturnRequest(
        UIManager uiManager,
        string targetSceneName,
        GamePlayDataManager gamePlayDataManager)
    {
        UiManager = uiManager;
        TargetSceneName = targetSceneName;
        GamePlayDataManager = gamePlayDataManager;
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(TargetSceneName);
}

internal readonly struct TitleReturnResult
{
    public readonly bool Succeeded;
    public readonly bool UsedSceneTransitionCoordinator;

    public TitleReturnResult(bool succeeded, bool usedSceneTransitionCoordinator)
    {
        Succeeded = succeeded;
        UsedSceneTransitionCoordinator = usedSceneTransitionCoordinator;
    }
}

internal static class TitleSceneNameResolver
{
    public static string Resolve(string titleSceneNameOverride)
    {
        if (!string.IsNullOrWhiteSpace(titleSceneNameOverride))
            return titleSceneNameOverride;

        string firstBuildScenePath = SceneUtility.GetScenePathByBuildIndex(0);
        if (!string.IsNullOrWhiteSpace(firstBuildScenePath))
            return Path.GetFileNameWithoutExtension(firstBuildScenePath);

        return null;
    }
}

internal static class TitleReturnService
{
    public static TitleReturnResult Execute(TitleReturnRequest request)
    {
        if (!request.IsValid)
            return new TitleReturnResult(false, false);

        request.UiManager?.CloseAllPopups();
        request.UiManager?.HideHoverImmediate();
        request.UiManager?.HideWorldPrompt();

        request.GamePlayDataManager?.EndRun(RunEndReason.None);

        SceneTransitionCoordinator transitionCoordinator = SceneTransitionCoordinator.EnsureInstance();
        if (transitionCoordinator != null && transitionCoordinator.TryLoadScene(request.TargetSceneName))
            return new TitleReturnResult(true, true);

        SceneManager.LoadScene(request.TargetSceneName);
        return new TitleReturnResult(true, false);
    }
}
