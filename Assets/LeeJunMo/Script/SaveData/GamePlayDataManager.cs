using System.Collections.Generic;
using UnityEngine;

public sealed class GamePlayDataManager : MonoBehaviour
{
    public static GamePlayDataManager Instance { get; private set; }

    public GamePlayData Data { get; private set; } = new GamePlayData();

    private static bool s_isQuitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting) return;
        if (Instance != null) return;

        var go = new GameObject(nameof(GamePlayDataManager));
        go.AddComponent<GamePlayDataManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void StartRun()
    {
        ClearPendingRunProgress();
        Data.isRunActive = true;
        Data.runElapsedSeconds = 0f;
    }

    public void EndRun(RunEndReason reason)
    {
        Data.lastRunEndReason = reason;
        if (Data.isRunActive)
        {
            CommitPendingRunProgress();
            GameDataSaveCoordinator.FlushNow(this);
        }

        Data.isRunActive = false;
        Data.pendingTransition = null;
        Data.pendingPlayerState = null;

        if (PortalRouteManager.Instance != null)
            PortalRouteManager.Instance.ClearPlan();
    }

    public int GetPendingRunMagicStoneDelta()
    {
        return Data != null ? Data.pendingRunMagicStoneDelta : 0;
    }

    public void AddPendingRunMagicStoneDelta(int delta)
    {
        if (Data == null || delta == 0)
            return;

        Data.pendingRunMagicStoneDelta += delta;
    }

    public void AddPendingAffectionDelta(int npcId, int delta)
    {
        if (Data == null || delta == 0)
            return;

        Data.pendingRunAffectionChanges ??= new List<PendingRunAffectionChange>();

        PendingRunAffectionChange existing = Data.pendingRunAffectionChanges.Find(x => x.npcId == npcId);
        if (existing != null)
        {
            existing.delta += delta;
            return;
        }

        Data.pendingRunAffectionChanges.Add(new PendingRunAffectionChange(npcId, delta));
    }

    public void AddPendingShortcutUnlock(string mapID, string doorID)
    {
        if (Data == null || string.IsNullOrWhiteSpace(mapID) || string.IsNullOrWhiteSpace(doorID))
            return;

        Data.pendingRunShortcutUnlocks ??= new List<PendingRunShortcutUnlock>();
        if (HasPendingShortcutUnlock(mapID, doorID))
            return;

        Data.pendingRunShortcutUnlocks.Add(new PendingRunShortcutUnlock(mapID, doorID));
    }

    public bool HasPendingShortcutUnlock(string mapID, string doorID)
    {
        if (Data?.pendingRunShortcutUnlocks == null)
            return false;

        return Data.pendingRunShortcutUnlocks.Exists(x => x.mapID == mapID && x.doorID == doorID);
    }

    public void TickRunTimer(float deltaTime)
    {
        if (!Data.isRunActive)
            return;

        Data.runElapsedSeconds += Mathf.Max(0f, deltaTime);
    }

    public void PrepareTransition(SceneTransitionContext context)
    {
        Data.pendingTransition = context;
    }

    public SceneTransitionContext PeekPendingTransition()
    {
        return Data.pendingTransition;
    }

    public SceneTransitionContext ConsumePendingTransition()
    {
        var ctx = Data.pendingTransition;
        Data.pendingTransition = null;
        return ctx;
    }

    public void PreparePlayerState(PlayerRuntimeState state)
    {
        Data.pendingPlayerState = state;
    }

    public void ClearPendingPlayerState()
    {
        Data.pendingPlayerState = null;
    }

    public PlayerRuntimeState PeekPendingPlayerState()
    {
        return Data.pendingPlayerState;
    }

    public PlayerRuntimeState ConsumePendingPlayerState()
    {
        var state = Data.pendingPlayerState;
        Data.pendingPlayerState = null;
        return state;
    }

    private void CommitPendingRunProgress()
    {
        if (Data == null || GameDataManager.Instance == null)
        {
            ClearPendingRunProgress();
            return;
        }

        GameData gameData = GameDataManager.Instance.EnsureData();

        if (Data.pendingRunMagicStoneDelta != 0)
            gameData.magicStone += Data.pendingRunMagicStoneDelta;

        CommitPendingAffectionChanges(gameData);
        CommitPendingShortcutUnlocks(gameData);
        ClearPendingRunProgress();
    }

    private void CommitPendingAffectionChanges(GameData gameData)
    {
        if (Data?.pendingRunAffectionChanges == null || Data.pendingRunAffectionChanges.Count == 0)
            return;

        gameData.affectionData ??= new AffectionSaveData();
        gameData.affectionData.affectionRecords ??= new List<AffectionRecord>();

        foreach (PendingRunAffectionChange change in Data.pendingRunAffectionChanges)
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

    private void CommitPendingShortcutUnlocks(GameData gameData)
    {
        if (Data?.pendingRunShortcutUnlocks == null || Data.pendingRunShortcutUnlocks.Count == 0)
            return;

        gameData.mapData ??= new MapSaveData();

        foreach (PendingRunShortcutUnlock unlock in Data.pendingRunShortcutUnlocks)
        {
            if (unlock == null || string.IsNullOrWhiteSpace(unlock.mapID) || string.IsNullOrWhiteSpace(unlock.doorID))
                continue;

            StageProgress stageData = gameData.mapData.GetStageData(unlock.mapID);
            if (!stageData.unlockedShortcuts.Contains(unlock.doorID))
                stageData.unlockedShortcuts.Add(unlock.doorID);
        }
    }

    private void ClearPendingRunProgress()
    {
        if (Data == null)
            return;

        Data.pendingRunMagicStoneDelta = 0;
        Data.pendingRunAffectionChanges ??= new List<PendingRunAffectionChange>();
        Data.pendingRunAffectionChanges.Clear();
        Data.pendingRunShortcutUnlocks ??= new List<PendingRunShortcutUnlock>();
        Data.pendingRunShortcutUnlocks.Clear();
        Data.merchantStates ??= new List<MerchantRuntimeState>();
        Data.merchantStates.Clear();
    }
}
