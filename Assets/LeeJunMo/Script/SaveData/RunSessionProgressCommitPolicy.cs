using System.Collections.Generic;
using UnityEngine;

internal readonly struct RunSessionProgressCommitRequest
{
    public readonly GamePlayData RunData;
    public readonly GameData PersistentData;

    public RunSessionProgressCommitRequest(GamePlayData runData, GameData persistentData)
    {
        RunData = runData;
        PersistentData = persistentData;
    }
}

internal static class RunSessionProgressCommitPolicy
{
    public static void Commit(RunSessionProgressCommitRequest request)
    {
        if (request.RunData == null || request.PersistentData == null)
        {
            ClearPendingRunProgress(request.RunData);
            return;
        }

        if (request.RunData.pendingRunMagicStoneDelta != 0)
            request.PersistentData.magicStone += request.RunData.pendingRunMagicStoneDelta;

        if (request.RunData.runElapsedSeconds > 0f)
            request.PersistentData.totalPlaySeconds += Mathf.Max(0f, request.RunData.runElapsedSeconds);

        if (request.RunData.lastRunEndReason == RunEndReason.Victory)
            request.PersistentData.clearCount += 1;

        CommitPendingAffectionChanges(request.RunData, request.PersistentData);
        CommitPendingShortcutUnlocks(request.RunData, request.PersistentData);
        ClearPendingRunProgress(request.RunData);
    }

    public static void ClearPendingRunProgress(GamePlayData runData)
    {
        if (runData == null)
            return;

        runData.pendingRunMagicStoneDelta = 0;
        runData.pendingRunAffectionChanges ??= new List<PendingRunAffectionChange>();
        runData.pendingRunAffectionChanges.Clear();
        runData.pendingRunShortcutUnlocks ??= new List<PendingRunShortcutUnlock>();
        runData.pendingRunShortcutUnlocks.Clear();
        runData.merchantStates ??= new List<MerchantRuntimeState>();
        runData.merchantStates.Clear();
    }

    private static void CommitPendingAffectionChanges(GamePlayData runData, GameData gameData)
    {
        if (runData.pendingRunAffectionChanges == null || runData.pendingRunAffectionChanges.Count == 0)
            return;

        gameData.affectionData ??= new AffectionSaveData();
        gameData.affectionData.affectionRecords ??= new List<AffectionRecord>();

        foreach (PendingRunAffectionChange change in runData.pendingRunAffectionChanges)
        {
            if (change == null || change.delta == 0)
                continue;

            AffectionRecord record = gameData.affectionData.affectionRecords.Find(x => x.npcId == change.npcId);
            if (record != null)
                record.amount += change.delta;
            else
                gameData.affectionData.affectionRecords.Add(new AffectionRecord(change.npcId, change.delta));
        }
    }

    private static void CommitPendingShortcutUnlocks(GamePlayData runData, GameData gameData)
    {
        if (runData.pendingRunShortcutUnlocks == null || runData.pendingRunShortcutUnlocks.Count == 0)
            return;

        gameData.mapData ??= new MapSaveData();

        foreach (PendingRunShortcutUnlock unlock in runData.pendingRunShortcutUnlocks)
        {
            if (unlock == null || string.IsNullOrWhiteSpace(unlock.mapID) || string.IsNullOrWhiteSpace(unlock.doorID))
                continue;

            StageProgress stageData = gameData.mapData.GetStageData(unlock.mapID);
            if (!stageData.unlockedShortcuts.Contains(unlock.doorID))
                stageData.unlockedShortcuts.Add(unlock.doorID);
        }
    }
}
