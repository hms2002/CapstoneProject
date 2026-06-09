[System.Serializable]
public sealed class PendingRunAffectionChange
{
    public int npcId;
    public int delta;

    public PendingRunAffectionChange(int npcId, int delta)
    {
        this.npcId = npcId;
        this.delta = delta;
    }
}

[System.Serializable]
public sealed class PendingRunShortcutUnlock
{
    public string mapID;
    public string doorID;

    public PendingRunShortcutUnlock(string mapID, string doorID)
    {
        this.mapID = mapID;
        this.doorID = doorID;
    }
}

[System.Serializable]
public sealed class PendingRunSpecialNpcConstructionStart
{
    public string constructionId;
    public int startedClearCount;

    public PendingRunSpecialNpcConstructionStart(string constructionId, int startedClearCount)
    {
        this.constructionId = constructionId;
        this.startedClearCount = startedClearCount;
    }
}

[System.Serializable]
public sealed class GamePlayData
{
    public bool isRunActive;
    public float runElapsedSeconds;
    public float runRemainingSeconds;
    public RunEndReason lastRunEndReason = RunEndReason.None;
    public bool pendingHubReturnFullHeal;
    public bool pendingHubLoadFullHeal;

    public SceneTransitionContext pendingTransition;
    public PlayerRuntimeState pendingPlayerState;

    public int pendingRunMagicStoneDelta;
    public System.Collections.Generic.List<PendingRunAffectionChange> pendingRunAffectionChanges = new System.Collections.Generic.List<PendingRunAffectionChange>();
    public System.Collections.Generic.List<PendingRunShortcutUnlock> pendingRunShortcutUnlocks = new System.Collections.Generic.List<PendingRunShortcutUnlock>();
    public System.Collections.Generic.List<PendingRunSpecialNpcConstructionStart> pendingRunSpecialNpcConstructionStarts = new System.Collections.Generic.List<PendingRunSpecialNpcConstructionStart>();
    public System.Collections.Generic.List<MerchantRuntimeState> merchantStates = new System.Collections.Generic.List<MerchantRuntimeState>();
}
