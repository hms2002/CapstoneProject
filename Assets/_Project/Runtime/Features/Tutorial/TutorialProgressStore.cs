// 책임: 튜토리얼 완료 여부를 저장 데이터에 조회/기록하고 즉시 저장 요청을 중계한다.
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
            GameDataStore.SaveData();

        return changed;
    }

    public static bool ClearCompleted(string tutorialId, bool saveImmediately = true)
    {
        if (!TryGetTutorialData(false, out TutorialSaveData tutorialData))
            return false;

        bool changed = tutorialData.ClearCompleted(tutorialId);
        if (changed && saveImmediately)
            GameDataStore.SaveData();

        return changed;
    }

    private static bool TryGetTutorialData(bool createManagerData, out TutorialSaveData tutorialData)
    {
        tutorialData = null;

        GameData data = createManagerData ? GameDataStore.EnsureData() : GameDataStore.Data;
        if (data == null)
            return false;

        data.tutorialData ??= new TutorialSaveData();
        data.tutorialData.Normalize();
        tutorialData = data.tutorialData;
        return true;
    }
}
