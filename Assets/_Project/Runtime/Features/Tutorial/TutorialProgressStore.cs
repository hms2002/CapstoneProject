public static class TutorialProgressStore
{
    public static bool IsCompleted(string tutorialId)
    {
        if (!TryGetTutorialData(false, out TutorialSaveData tutorialData))
            return false;

        return tutorialData.IsCompleted(tutorialId);
    }

    public static bool MarkCompleted(string tutorialId, bool saveImmediately = true)
    {
        if (!TryGetTutorialData(true, out TutorialSaveData tutorialData))
            return false;

        bool changed = tutorialData.MarkCompleted(tutorialId);
        if (changed && saveImmediately)
            GameDataManager.Instance.SaveData();

        return changed;
    }

    public static bool ClearCompleted(string tutorialId, bool saveImmediately = true)
    {
        if (!TryGetTutorialData(false, out TutorialSaveData tutorialData))
            return false;

        bool changed = tutorialData.ClearCompleted(tutorialId);
        if (changed && saveImmediately)
            GameDataManager.Instance.SaveData();

        return changed;
    }

    private static bool TryGetTutorialData(bool createManagerData, out TutorialSaveData tutorialData)
    {
        tutorialData = null;

        GameDataManager manager = GameDataManager.Instance;
        if (manager == null)
            return false;

        GameData data = createManagerData ? manager.EnsureData() : manager.Data;
        if (data == null)
            return false;

        data.tutorialData ??= new TutorialSaveData();
        data.tutorialData.Normalize();
        tutorialData = data.tutorialData;
        return true;
    }
}
