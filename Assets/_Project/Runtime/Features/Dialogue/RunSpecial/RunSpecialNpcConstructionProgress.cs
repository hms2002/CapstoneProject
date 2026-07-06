using UnityEngine;

// 책임: 런 특수 NPC 건설 진행도의 시작/완료 상태를 런 클리어 카운트 기준으로 계산한다.
public static class RunSpecialNpcConstructionProgress
{
    public static bool HasStarted(string constructionId)
    {
        return TryGetStartedClearCount(constructionId, out _);
    }

    public static bool IsCompleted(string constructionId, int requiredRunCompletions)
    {
        if (!TryGetRecord(constructionId, out RunSpecialNpcConstructionRecord record))
        {
            if (!TryGetStartedClearCount(constructionId, out int pendingStartedClearCount))
                return false;

            return HasRequiredRunsPassed(pendingStartedClearCount, requiredRunCompletions);
        }

        return record.completed || HasRequiredRunsPassed(record.startedClearCount, requiredRunCompletions);
    }

    public static bool IsMarkedCompleted(string constructionId)
    {
        return TryGetRecord(constructionId, out RunSpecialNpcConstructionRecord record) && record.completed;
    }

    public static int GetRemainingRunCompletions(string constructionId, int requiredRunCompletions)
    {
        if (!TryGetStartedClearCount(constructionId, out int startedClearCount))
            return Mathf.Max(0, requiredRunCompletions);

        int completedRuns = GetCurrentClearCount() - startedClearCount;
        return Mathf.Max(0, requiredRunCompletions - Mathf.Max(0, completedRuns));
    }

    public static bool TryStart(string constructionId, out int startedClearCount)
    {
        startedClearCount = GetCurrentClearCount();

        if (string.IsNullOrWhiteSpace(constructionId) || HasStarted(constructionId))
            return false;

        if (IsRunActive())
        {
            RunSessionStore.AddPendingRunSpecialNpcConstructionStart(
                constructionId,
                startedClearCount);
            return true;
        }

        if (!TryGetSaveData(out RunSpecialNpcSaveData saveData))
            return false;

        saveData.GetOrCreateConstructionRecord(constructionId, startedClearCount);
        GameDataStore.RequestImmediateSave();
        return true;
    }

    public static void MarkCompleted(string constructionId)
    {
        if (string.IsNullOrWhiteSpace(constructionId))
            return;

        if (!TryGetSaveData(out RunSpecialNpcSaveData saveData))
            return;

        RunSpecialNpcConstructionRecord record =
            saveData.GetOrCreateConstructionRecord(constructionId, GetCurrentClearCount());
        if (record.completed)
            return;

        record.completed = true;
        GameDataStore.RequestImmediateSave();
    }

    public static bool TryGetStartedClearCount(string constructionId, out int startedClearCount)
    {
        startedClearCount = 0;

        if (TryGetRecord(constructionId, out RunSpecialNpcConstructionRecord record))
        {
            startedClearCount = record.startedClearCount;
            return true;
        }

        if (IsRunActive() &&
            RunSessionStore.TryGetPendingRunSpecialNpcConstructionStart(
                constructionId,
                out int pendingStartedClearCount))
        {
            startedClearCount = pendingStartedClearCount;
            return true;
        }

        return false;
    }

    private static bool TryGetRecord(string constructionId, out RunSpecialNpcConstructionRecord record)
    {
        record = null;

        if (string.IsNullOrWhiteSpace(constructionId) || !TryGetSaveData(out RunSpecialNpcSaveData saveData))
            return false;

        record = saveData.FindConstructionRecord(constructionId);
        return record != null;
    }

    private static bool TryGetSaveData(out RunSpecialNpcSaveData saveData)
    {
        saveData = null;

        GameData data = GameDataStore.EnsureData();
        if (data == null)
            return false;

        data.runSpecialNpcData ??= new RunSpecialNpcSaveData();
        data.runSpecialNpcData.constructionRecords ??= new System.Collections.Generic.List<RunSpecialNpcConstructionRecord>();
        saveData = data.runSpecialNpcData;
        return true;
    }

    private static int GetCurrentClearCount()
    {
        return GameDataStore.Data != null
            ? Mathf.Max(0, GameDataStore.Data.clearCount)
            : 0;
    }

    private static bool HasRequiredRunsPassed(int startedClearCount, int requiredRunCompletions)
    {
        int required = Mathf.Max(0, requiredRunCompletions);
        int completedRuns = GetCurrentClearCount() - Mathf.Max(0, startedClearCount);
        return completedRuns >= required;
    }

    private static bool IsRunActive()
    {
        return RunSessionStore.IsRunActive;
    }
}
