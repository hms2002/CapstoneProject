using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 런 세션 상태의 구체 저장소/수명 구현을 Core/Gameplay 호출자에게 숨기는 backend 계약이다.
/// </summary>
public interface IRunSessionStoreBackend
{
    GamePlayData Data { get; }
    event Action OnRunStarted;
    event Action<RunEndReason> OnRunEnded;

    void StartRun();
    void EndRun(RunEndReason reason);
    void SetRunRemainingSeconds(float remainingSeconds);
    float GetRunRemainingSeconds();
    int GetPendingRunMagicStoneDelta();
    void AddPendingRunMagicStoneDelta(int delta);
    void AddPendingAffectionDelta(int npcId, int delta);
    void AddPendingShortcutUnlock(string mapID, string doorID);
    bool HasPendingShortcutUnlock(string mapID, string doorID);
    void AddPendingRunSpecialNpcConstructionStart(string constructionId, int startedClearCount);
    bool TryGetPendingRunSpecialNpcConstructionStart(string constructionId, out int startedClearCount);
    void TickRunTimer(float deltaTime);
    void PrepareTransition(SceneTransitionContext context);
    SceneTransitionContext PeekPendingTransition();
    SceneTransitionContext ConsumePendingTransition();
    void PreparePlayerState(PlayerRuntimeState state);
    void ClearPendingPlayerState();
    bool ConsumePendingHubReturnFullHeal();
    void RequestPendingHubLoadFullHeal();
    bool ConsumePendingHubLoadFullHeal();
    PlayerRuntimeState PeekPendingPlayerState();
    PlayerRuntimeState ConsumePendingPlayerState();
    int ResolveDungeonSeed(string dungeonId, DungeonReentryPolicy policy, int fallbackSeed);
    bool TryGetDungeonObjectStates(string dungeonId, List<DungeonObjectRuntimeStateData> destination);
    void SaveDungeonObjectStates(string dungeonId, IReadOnlyList<DungeonObjectRuntimeStateData> states);
    void ResetForDevelopmentStart();
}

/// <summary>
/// 책임 : Core/Gameplay 코드가 Infrastructure 런 세션 매니저 타입 없이 현재 런 상태를 조회하고 명령을 보낸다.
/// </summary>
public static class RunSessionStore
{
    private static IRunSessionStoreBackend backend;

    public static event Action OnRunStarted;
    public static event Action<RunEndReason> OnRunEnded;

    public static bool IsAvailable => IsBackendAlive(backend);
    public static GamePlayData Data => IsAvailable ? backend.Data : null;
    public static bool IsRunActive => Data != null && Data.isRunActive;

    public static void RegisterBackend(IRunSessionStoreBackend storeBackend)
    {
        if (ReferenceEquals(backend, storeBackend))
            return;

        UnsubscribeBackend();
        backend = storeBackend;
        SubscribeBackend();
    }

    public static void UnregisterBackend(IRunSessionStoreBackend storeBackend)
    {
        if (!ReferenceEquals(backend, storeBackend))
            return;

        UnsubscribeBackend();
        backend = null;
    }

    public static void StartRun()
    {
        if (IsAvailable)
            backend.StartRun();
    }

    public static void EndRun(RunEndReason reason)
    {
        if (IsAvailable)
            backend.EndRun(reason);
    }

    public static void SetRunRemainingSeconds(float remainingSeconds)
    {
        if (IsAvailable)
            backend.SetRunRemainingSeconds(remainingSeconds);
    }

    public static float GetRunRemainingSeconds()
    {
        return IsAvailable ? backend.GetRunRemainingSeconds() : 0f;
    }

    public static int GetPendingRunMagicStoneDelta()
    {
        return IsAvailable ? backend.GetPendingRunMagicStoneDelta() : 0;
    }

    public static void AddPendingRunMagicStoneDelta(int delta)
    {
        if (IsAvailable)
            backend.AddPendingRunMagicStoneDelta(delta);
    }

    public static void AddPendingAffectionDelta(int npcId, int delta)
    {
        if (IsAvailable)
            backend.AddPendingAffectionDelta(npcId, delta);
    }

    public static void AddPendingShortcutUnlock(string mapID, string doorID)
    {
        if (IsAvailable)
            backend.AddPendingShortcutUnlock(mapID, doorID);
    }

    public static bool HasPendingShortcutUnlock(string mapID, string doorID)
    {
        return IsAvailable && backend.HasPendingShortcutUnlock(mapID, doorID);
    }

    public static void AddPendingRunSpecialNpcConstructionStart(string constructionId, int startedClearCount)
    {
        if (IsAvailable)
            backend.AddPendingRunSpecialNpcConstructionStart(constructionId, startedClearCount);
    }

    public static bool TryGetPendingRunSpecialNpcConstructionStart(string constructionId, out int startedClearCount)
    {
        startedClearCount = 0;
        return IsAvailable && backend.TryGetPendingRunSpecialNpcConstructionStart(constructionId, out startedClearCount);
    }

    public static void TickRunTimer(float deltaTime)
    {
        if (IsAvailable)
            backend.TickRunTimer(deltaTime);
    }

    public static void PrepareTransition(SceneTransitionContext context)
    {
        if (IsAvailable)
            backend.PrepareTransition(context);
    }

    public static SceneTransitionContext PeekPendingTransition()
    {
        return IsAvailable ? backend.PeekPendingTransition() : null;
    }

    public static SceneTransitionContext ConsumePendingTransition()
    {
        return IsAvailable ? backend.ConsumePendingTransition() : null;
    }

    public static void PreparePlayerState(PlayerRuntimeState state)
    {
        if (IsAvailable)
            backend.PreparePlayerState(state);
    }

    public static void ClearPendingPlayerState()
    {
        if (IsAvailable)
            backend.ClearPendingPlayerState();
    }

    public static bool ConsumePendingHubReturnFullHeal()
    {
        return IsAvailable && backend.ConsumePendingHubReturnFullHeal();
    }

    public static void RequestPendingHubLoadFullHeal()
    {
        if (IsAvailable)
            backend.RequestPendingHubLoadFullHeal();
    }

    public static bool ConsumePendingHubLoadFullHeal()
    {
        return IsAvailable && backend.ConsumePendingHubLoadFullHeal();
    }

    public static PlayerRuntimeState PeekPendingPlayerState()
    {
        return IsAvailable ? backend.PeekPendingPlayerState() : null;
    }

    public static PlayerRuntimeState ConsumePendingPlayerState()
    {
        return IsAvailable ? backend.ConsumePendingPlayerState() : null;
    }

    public static int ResolveDungeonSeed(
        string dungeonId,
        DungeonReentryPolicy policy,
        int fallbackSeed)
    {
        return IsAvailable
            ? backend.ResolveDungeonSeed(dungeonId, policy, fallbackSeed)
            : fallbackSeed;
    }

    public static bool TryGetDungeonObjectStates(
        string dungeonId,
        List<DungeonObjectRuntimeStateData> destination)
    {
        return IsAvailable && backend.TryGetDungeonObjectStates(dungeonId, destination);
    }

    public static void SaveDungeonObjectStates(
        string dungeonId,
        IReadOnlyList<DungeonObjectRuntimeStateData> states)
    {
        if (IsAvailable)
            backend.SaveDungeonObjectStates(dungeonId, states);
    }

    public static void ResetForDevelopmentStart()
    {
        if (IsAvailable)
            backend.ResetForDevelopmentStart();
    }

    private static void SubscribeBackend()
    {
        if (backend == null)
            return;

        backend.OnRunStarted += RelayRunStarted;
        backend.OnRunEnded += RelayRunEnded;
    }

    private static void UnsubscribeBackend()
    {
        if (backend == null)
            return;

        backend.OnRunStarted -= RelayRunStarted;
        backend.OnRunEnded -= RelayRunEnded;
    }

    private static void RelayRunStarted()
    {
        OnRunStarted?.Invoke();
    }

    private static void RelayRunEnded(RunEndReason reason)
    {
        OnRunEnded?.Invoke(reason);
    }

    private static bool IsBackendAlive(IRunSessionStoreBackend storeBackend)
    {
        if (storeBackend == null)
            return false;

        if (storeBackend is Component component)
            return component != null;

        return true;
    }
}
