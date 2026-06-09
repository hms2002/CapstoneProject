using UnityEngine;

public static class BossDialogueProgressStore
{
    public static int GetEncounterCount(int npcId)
    {
        BossDialogueRecord record = GetRecord(npcId);
        return record != null ? Mathf.Max(0, record.encounterCount) : 0;
    }

    public static int GetVictoryCount(int npcId)
    {
        BossDialogueRecord record = GetRecord(npcId);
        return record != null ? Mathf.Max(0, record.victoryCount) : 0;
    }

    public static int GetDefeatCount(int npcId)
    {
        BossDialogueRecord record = GetRecord(npcId);
        return record != null ? Mathf.Max(0, record.defeatCount) : 0;
    }

    public static void RegisterEncounter(NPCData npcData)
    {
        if (npcData == null)
            return;

        BossDialogueRecord record = GetOrCreateRecord(npcData.id);
        if (record == null)
            return;

        record.encounterCount = Mathf.Max(0, record.encounterCount) + 1;
        record.lastEncounterTotalPlaySeconds = GetCurrentTotalPlaySeconds();
        RequestSaveIfSafe();
    }

    public static void RegisterVictory(NPCData npcData)
    {
        if (npcData == null)
            return;

        BossDialogueRecord record = GetOrCreateRecord(npcData.id);
        if (record == null)
            return;

        record.victoryCount = Mathf.Max(0, record.victoryCount) + 1;
        RequestSaveIfSafe();
    }

    public static void RegisterDefeat(NPCData npcData)
    {
        if (npcData == null)
            return;

        BossDialogueRecord record = GetOrCreateRecord(npcData.id);
        if (record == null)
            return;

        record.defeatCount = Mathf.Max(0, record.defeatCount) + 1;
        RequestSaveIfSafe();
    }

    private static BossDialogueRecord GetRecord(int npcId)
    {
        GameData data = GameDataManager.Instance != null ? GameDataManager.Instance.Data : null;
        if (data == null || data.bossDialogueData == null || data.bossDialogueData.bossRecords == null)
            return null;

        return data.bossDialogueData.bossRecords.Find(x => x != null && x.npcId == npcId);
    }

    private static BossDialogueRecord GetOrCreateRecord(int npcId)
    {
        if (GameDataManager.Instance == null)
            return null;

        GameData data = GameDataManager.Instance.EnsureData();
        data.bossDialogueData ??= new BossDialogueSaveData();
        return data.bossDialogueData.GetOrCreateRecord(npcId);
    }

    private static float GetCurrentTotalPlaySeconds()
    {
        float total = GameDataManager.Instance != null && GameDataManager.Instance.Data != null
            ? Mathf.Max(0f, GameDataManager.Instance.Data.totalPlaySeconds)
            : 0f;

        GamePlayData gameplayData = GamePlayDataManager.Instance != null
            ? GamePlayDataManager.Instance.Data
            : null;

        if (gameplayData != null && gameplayData.isRunActive)
            total += Mathf.Max(0f, gameplayData.runElapsedSeconds);

        return total;
    }

    private static void RequestSaveIfSafe()
    {
        GamePlayData gameplayData = GamePlayDataManager.Instance != null
            ? GamePlayDataManager.Instance.Data
            : null;

        if (gameplayData != null && gameplayData.isRunActive)
            return;

        GameDataSaveCoordinator.RequestImmediateSave();
    }
}
