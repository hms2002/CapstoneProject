/// <summary>
/// 책임 : 현재 런에서 누적된 NPC 호감도 변경량을 저장하는 직렬화 DTO다.
/// </summary>
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

/// <summary>
/// 책임 : 현재 런에서 해금 대기 중인 숏컷 정보를 저장하는 직렬화 DTO다.
/// </summary>
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

/// <summary>
/// 책임 : 현재 런에서 시작된 특수 NPC 건설 진행 정보를 저장하는 직렬화 DTO다.
/// </summary>
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

/// <summary>
/// 책임 : 런 진행 중인 임시 상태와 허브 복귀 시 커밋할 대기 변경분을 보관하는 직렬화 루트 DTO다.
/// </summary>
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
