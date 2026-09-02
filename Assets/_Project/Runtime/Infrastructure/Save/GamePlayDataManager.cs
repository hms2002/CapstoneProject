using System;
using System.Collections.Generic;
using UnityEngine;

// 책임: 런 세션 데이터를 유지하고 Core RunSessionStore 백엔드를 등록해 런 시작/종료 상태를 중계한다.
public sealed class GamePlayDataManager : MonoBehaviour, IRunSessionStoreBackend
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
        RunSessionStore.RegisterBackend(this);
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    private void OnDestroy()
    {
        RunSessionStore.UnregisterBackend(this);

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
        RunSessionStateService.SetRunRemainingSeconds(Data, remainingSeconds);
    }

    /// <summary>
    /// 梨낆엫 :
    /// - ?꾩옱 ?곗뿉 ??λ맂 ?⑥? ?쒗븳 ?쒓컙??議고쉶?쒕떎.
    /// - ????대㉧ ?쒖뒪?쒖씠 ???꾪솚 ??蹂듭썝??湲곗?媛믪쓣 ?쎌쓣 ???덇쾶 ?쒕떎.
    /// </summary>
    public float GetRunRemainingSeconds()
    {
        return RunSessionStateService.GetRunRemainingSeconds(Data);
    }

    public int GetPendingRunMagicStoneDelta()
    {
        return RunSessionStateService.GetPendingRunMagicStoneDelta(Data);
    }

    public void AddPendingRunMagicStoneDelta(int delta)
    {
        RunSessionStateService.AddPendingRunMagicStoneDelta(Data, delta);
    }

    public void AddPendingAffectionDelta(int npcId, int delta)
    {
        RunSessionStateService.AddPendingAffectionDelta(Data, npcId, delta);
    }

    public void AddPendingShortcutUnlock(string mapID, string doorID)
    {
        RunSessionStateService.AddPendingShortcutUnlock(Data, mapID, doorID);
    }

    public bool HasPendingShortcutUnlock(string mapID, string doorID)
    {
        return RunSessionStateService.HasPendingShortcutUnlock(Data, mapID, doorID);
    }

    public void AddPendingRunSpecialNpcConstructionStart(string constructionId, int startedClearCount)
    {
        RunSessionStateService.AddPendingRunSpecialNpcConstructionStart(Data, constructionId, startedClearCount);
    }

    public bool TryGetPendingRunSpecialNpcConstructionStart(string constructionId, out int startedClearCount)
    {
        return RunSessionStateService.TryGetPendingRunSpecialNpcConstructionStart(Data, constructionId, out startedClearCount);
    }

    public void TickRunTimer(float deltaTime)
    {
        RunSessionStateService.TickRunTimer(Data, deltaTime);
    }

    public void PrepareTransition(SceneTransitionContext context)
    {
        RunSessionStateService.PrepareTransition(Data, context);
    }

    public SceneTransitionContext PeekPendingTransition()
    {
        return RunSessionStateService.PeekPendingTransition(Data);
    }

    public SceneTransitionContext ConsumePendingTransition()
    {
        return RunSessionStateService.ConsumePendingTransition(Data);
    }

    public void PreparePlayerState(PlayerRuntimeState state)
    {
        RunSessionStateService.PreparePlayerState(Data, state);
    }

    public void ClearPendingPlayerState()
    {
        RunSessionStateService.ClearPendingPlayerState(Data);
    }

    public bool ConsumePendingHubReturnFullHeal()
    {
        return RunSessionStateService.ConsumePendingHubReturnFullHeal(Data);
    }

    public void RequestPendingHubLoadFullHeal()
    {
        RunSessionStateService.RequestPendingHubLoadFullHeal(Data);
    }

    public bool ConsumePendingHubLoadFullHeal()
    {
        return RunSessionStateService.ConsumePendingHubLoadFullHeal(Data);
    }

    public PlayerRuntimeState PeekPendingPlayerState()
    {
        return RunSessionStateService.PeekPendingPlayerState(Data);
    }

    public PlayerRuntimeState ConsumePendingPlayerState()
    {
        return RunSessionStateService.ConsumePendingPlayerState(Data);
    }

    public int ResolveDungeonSeed(
        string dungeonId,
        DungeonReentryPolicy policy,
        int fallbackSeed)
    {
        return RunSessionStateService.ResolveDungeonSeed(Data, dungeonId, policy, fallbackSeed);
    }

    public bool TryGetDungeonObjectStates(
        string dungeonId,
        List<DungeonObjectRuntimeStateData> destination)
    {
        return RunSessionStateService.TryGetDungeonObjectStates(Data, dungeonId, destination);
    }

    public void SaveDungeonObjectStates(
        string dungeonId,
        IReadOnlyList<DungeonObjectRuntimeStateData> states)
    {
        RunSessionStateService.SaveDungeonObjectStates(Data, dungeonId, states);
    }

    public bool TryGetDungeonMapDiscovery(
        string dungeonId,
        List<int> visitedRoomPlacementIds,
        List<int> revealedRoomPlacementIds)
    {
        return RunSessionStateService.TryGetDungeonMapDiscovery(
            Data,
            dungeonId,
            visitedRoomPlacementIds,
            revealedRoomPlacementIds);
    }

    public void SaveDungeonMapDiscovery(
        string dungeonId,
        IReadOnlyList<int> visitedRoomPlacementIds,
        IReadOnlyList<int> revealedRoomPlacementIds)
    {
        RunSessionStateService.SaveDungeonMapDiscovery(
            Data,
            dungeonId,
            visitedRoomPlacementIds,
            revealedRoomPlacementIds);
    }

    public void ResetForDevelopmentStart()
    {
        Data ??= new GamePlayData();
        RunSessionStateService.ResetForDevelopmentStart(Data, ClearPendingRunProgress);
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
