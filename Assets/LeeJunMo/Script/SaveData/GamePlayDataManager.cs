using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GamePlayDataManager : MonoBehaviour
{
    public static GamePlayDataManager Instance { get; private set; }

    public GamePlayData Data { get; private set; } = new GamePlayData();

    public event Action OnRunStarted;
    public event Action<RunEndReason> OnRunEnded;

    private static bool s_isQuitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting)
            return;

        EnsureInstance();
    }

    public static GamePlayDataManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GamePlayDataManager existing = FindFirstObjectByType<GamePlayDataManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        if (s_isQuitting)
            return null;

        var go = new GameObject(nameof(GamePlayDataManager));
        return go.AddComponent<GamePlayDataManager>();
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
        Data.runRemainingSeconds = 0f;
        OnRunStarted?.Invoke();
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
        Data.runRemainingSeconds = 0f;
        Data.pendingTransition = null;
        Data.pendingPlayerState = null;

        if (PortalRouteManager.Instance != null)
            PortalRouteManager.Instance.ClearPlan();

        OnRunEnded?.Invoke(reason);
    }

    /// <summary>
    /// 책임 :
    /// - 런 진행 중 남은 제한 시간을 GamePlayData에 동기화한다.
    /// - 씬 전환 이후에도 런 타이머 상태가 이어지도록 중앙 저장소 역할을 맡는다.
    /// </summary>
    public void SetRunRemainingSeconds(float remainingSeconds)
    {
        if (Data == null)
            return;

        Data.runRemainingSeconds = Mathf.Max(0f, remainingSeconds);
    }

    /// <summary>
    /// 책임 :
    /// - 현재 런에 저장된 남은 제한 시간을 조회한다.
    /// - 런 타이머 시스템이 씬 전환 후 복원할 기준값을 읽을 수 있게 한다.
    /// </summary>
    public float GetRunRemainingSeconds()
    {
        return Data != null ? Mathf.Max(0f, Data.runRemainingSeconds) : 0f;
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

    public void ResetForDevelopmentStart()
    {
        Data ??= new GamePlayData();
        Data.isRunActive = false;
        Data.runElapsedSeconds = 0f;
        Data.runRemainingSeconds = 0f;
        Data.lastRunEndReason = RunEndReason.None;
        Data.pendingTransition = null;
        Data.pendingPlayerState = null;
        ClearPendingRunProgress();
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

        if (Data.runElapsedSeconds > 0f)
            gameData.totalPlaySeconds += Mathf.Max(0f, Data.runElapsedSeconds);

        if (Data.lastRunEndReason == RunEndReason.Victory)
            gameData.clearCount += 1;

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
