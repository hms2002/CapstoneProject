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
/// 책임 : PreserveDuringRun 던전의 개봉 상자에서 아직 획득하지 않은 슬롯 아이템과 유물 레벨을 저장한다.
/// </summary>
[System.Serializable]
public sealed class DungeonChestLootRuntimeStateData
{
    public int slotIndex;
    public UnityEngine.ScriptableObject item;
    public int relicLevel;
}

/// <summary>
/// 책임 : PreserveDuringRun 던전에서 안정적인 배치 Id별 생존·활성·상자 개봉 및 잔여 보상 상태를 저장한다.
/// </summary>
[System.Serializable]
public sealed class DungeonObjectRuntimeStateData
{
    public string stateId;
    public bool isPresent = true;
    public bool isActive = true;
    public bool isChestOpened;
    public System.Collections.Generic.List<DungeonChestLootRuntimeStateData> chestLoot =
        new System.Collections.Generic.List<DungeonChestLootRuntimeStateData>();
}

/// <summary>
/// 책임 : 한 절차 던전의 현재 런 레이아웃 seed와 선택적으로 보존할 생성 오브젝트 상태를 묶어 저장한다.
/// </summary>
[System.Serializable]
public sealed class DungeonRunStateData
{
    public string dungeonId;
    public bool hasResolvedSeed;
    public int resolvedSeed;
    public System.Collections.Generic.List<DungeonObjectRuntimeStateData> objectStates =
        new System.Collections.Generic.List<DungeonObjectRuntimeStateData>();
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

    public System.Collections.Generic.List<string> defeatedBossIds = new System.Collections.Generic.List<string>();
    public System.Collections.Generic.List<DungeonRunStateData> dungeonRunStates = new System.Collections.Generic.List<DungeonRunStateData>();

    public int pendingRunMagicStoneDelta;
    public System.Collections.Generic.List<PendingRunAffectionChange> pendingRunAffectionChanges = new System.Collections.Generic.List<PendingRunAffectionChange>();
    public System.Collections.Generic.List<PendingRunShortcutUnlock> pendingRunShortcutUnlocks = new System.Collections.Generic.List<PendingRunShortcutUnlock>();
    public System.Collections.Generic.List<PendingRunSpecialNpcConstructionStart> pendingRunSpecialNpcConstructionStarts = new System.Collections.Generic.List<PendingRunSpecialNpcConstructionStart>();
    public System.Collections.Generic.List<MerchantRuntimeState> merchantStates = new System.Collections.Generic.List<MerchantRuntimeState>();
}
