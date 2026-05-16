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
        RunSessionLifecycleService.StartRun(Data, ClearPendingRunProgress);
        OnRunStarted?.Invoke();
    }

    public void EndRun(RunEndReason reason)
    {
        RunSessionLifecycleService.EndRun(
            Data,
            reason,
            CommitPendingRunProgress,
            () => PortalRouteManager.Instance?.ClearPlan(),
            this);
        OnRunEnded?.Invoke(reason);
    }

    /// <summary>
    /// 梨낆엫 :
    /// - ??吏꾪뻾 以??⑥? ?쒗븳 ?쒓컙??GamePlayData???숆린?뷀븳??
    /// - ???꾪솚 ?댄썑?먮룄 ????대㉧ ?곹깭媛 ?댁뼱吏?꾨줉 以묒븰 ??μ냼 ??븷??留〓뒗??
    /// </summary>
    public void SetRunRemainingSeconds(float remainingSeconds)
    {
        if (Data == null)
            return;

        Data.runRemainingSeconds = Mathf.Max(0f, remainingSeconds);
    }

    /// <summary>
    /// 梨낆엫 :
    /// - ?꾩옱 ?곗뿉 ??λ맂 ?⑥? ?쒗븳 ?쒓컙??議고쉶?쒕떎.
    /// - ????대㉧ ?쒖뒪?쒖씠 ???꾪솚 ??蹂듭썝??湲곗?媛믪쓣 ?쎌쓣 ???덇쾶 ?쒕떎.
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
        RunSessionProgressCommitPolicy.Commit(new RunSessionProgressCommitRequest(Data, gameData));
    }

    private void ClearPendingRunProgress()
    {
        RunSessionProgressCommitPolicy.ClearPendingRunProgress(Data);
    }
}
