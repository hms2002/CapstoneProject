/// <summary>Run-owned officer objective progress; HUD only acknowledges its completion presentation.</summary>
public static class RunOfficerQuestProgress
{
    public static readonly string[] BossIds = { "slime", "dragon", "shadow" };

    public static int CountDefeated(GamePlayData data)
    {
        if (data == null || !data.isRunActive) return 0;
        int count = 0;
        foreach (string id in BossIds)
            if (data.defeatedBossIds?.Contains(id) == true) count++;
        return count;
    }

    public static void EnterScene(GamePlayData data, string sceneName)
    {
        if (data != null && data.isRunActive && sceneName == "Grand Hall")
            data.officerQuestStarted = true;
    }

    public static bool IsVisible(GamePlayData data) =>
        data != null && data.isRunActive && data.officerQuestStarted && !data.officerQuestCompletionPresented;

    public static bool CanPresentCompletion(GamePlayData data, string sceneName) =>
        IsVisible(data) && CountDefeated(data) == 3 && sceneName == "Grand Hall";

    public static void CompletePresentation(GamePlayData data, string sceneName)
    {
        if (CanPresentCompletion(data, sceneName)) data.officerQuestCompletionPresented = true;
    }
}
