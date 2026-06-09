using System;
using System.Collections.Generic;
using UnityEngine;

internal static class RunSessionLifecycleService
{
    public static void StartRun(GamePlayData data, Action clearPendingRunProgress)
    {
        if (data == null)
            return;

        clearPendingRunProgress?.Invoke();
        data.isRunActive = true;
        data.runElapsedSeconds = 0f;
        data.runRemainingSeconds = 0f;
        data.pendingHubReturnFullHeal = false;
        data.pendingHubLoadFullHeal = false;
    }

    public static void EndRun(
        GamePlayData data,
        RunEndReason reason,
        Action commitPendingRunProgress,
        Action clearRoutePlan,
        UnityEngine.Object saveRequester)
    {
        if (data == null)
            return;

        data.lastRunEndReason = reason;
        if (data.isRunActive)
        {
            commitPendingRunProgress?.Invoke();
            GameDataSaveCoordinator.FlushNow(saveRequester);
        }

        data.isRunActive = false;
        data.runRemainingSeconds = 0f;
        data.pendingTransition = null;
        data.pendingPlayerState = null;
        data.pendingHubReturnFullHeal = reason != RunEndReason.None;
        clearRoutePlan?.Invoke();
    }
}

internal static class RunSessionStateService
{
    public static void SetRunRemainingSeconds(GamePlayData data, float remainingSeconds)
    {
        if (data == null)
            return;

        data.runRemainingSeconds = Mathf.Max(0f, remainingSeconds);
    }

    public static float GetRunRemainingSeconds(GamePlayData data)
    {
        return data != null ? Mathf.Max(0f, data.runRemainingSeconds) : 0f;
    }

    public static int GetPendingRunMagicStoneDelta(GamePlayData data)
    {
        return data != null ? data.pendingRunMagicStoneDelta : 0;
    }

    public static void AddPendingRunMagicStoneDelta(GamePlayData data, int delta)
    {
        if (data == null || delta == 0)
            return;

        data.pendingRunMagicStoneDelta += delta;
    }

    public static void AddPendingAffectionDelta(GamePlayData data, int npcId, int delta)
    {
        if (data == null || delta == 0)
            return;

        data.pendingRunAffectionChanges ??= new List<PendingRunAffectionChange>();

        PendingRunAffectionChange existing = data.pendingRunAffectionChanges.Find(x => x.npcId == npcId);
        if (existing != null)
        {
            existing.delta += delta;
            return;
        }

        data.pendingRunAffectionChanges.Add(new PendingRunAffectionChange(npcId, delta));
    }

    public static void AddPendingShortcutUnlock(GamePlayData data, string mapID, string doorID)
    {
        if (data == null || string.IsNullOrWhiteSpace(mapID) || string.IsNullOrWhiteSpace(doorID))
            return;

        data.pendingRunShortcutUnlocks ??= new List<PendingRunShortcutUnlock>();
        if (HasPendingShortcutUnlock(data, mapID, doorID))
            return;

        data.pendingRunShortcutUnlocks.Add(new PendingRunShortcutUnlock(mapID, doorID));
    }

    public static bool HasPendingShortcutUnlock(GamePlayData data, string mapID, string doorID)
    {
        if (data?.pendingRunShortcutUnlocks == null)
            return false;

        return data.pendingRunShortcutUnlocks.Exists(x => x.mapID == mapID && x.doorID == doorID);
    }

    public static void AddPendingRunSpecialNpcConstructionStart(
        GamePlayData data,
        string constructionId,
        int startedClearCount)
    {
        if (data == null || string.IsNullOrWhiteSpace(constructionId))
            return;

        data.pendingRunSpecialNpcConstructionStarts ??= new List<PendingRunSpecialNpcConstructionStart>();

        PendingRunSpecialNpcConstructionStart existing =
            data.pendingRunSpecialNpcConstructionStarts.Find(x => x != null && x.constructionId == constructionId);
        if (existing != null)
        {
            existing.startedClearCount = Mathf.Min(existing.startedClearCount, startedClearCount);
            return;
        }

        data.pendingRunSpecialNpcConstructionStarts.Add(
            new PendingRunSpecialNpcConstructionStart(constructionId, startedClearCount));
    }

    public static bool TryGetPendingRunSpecialNpcConstructionStart(
        GamePlayData data,
        string constructionId,
        out int startedClearCount)
    {
        startedClearCount = 0;

        if (data?.pendingRunSpecialNpcConstructionStarts == null || string.IsNullOrWhiteSpace(constructionId))
            return false;

        PendingRunSpecialNpcConstructionStart pending =
            data.pendingRunSpecialNpcConstructionStarts.Find(x => x != null && x.constructionId == constructionId);
        if (pending == null)
            return false;

        startedClearCount = pending.startedClearCount;
        return true;
    }

    public static void TickRunTimer(GamePlayData data, float deltaTime)
    {
        if (data == null || !data.isRunActive)
            return;

        data.runElapsedSeconds += Mathf.Max(0f, deltaTime);
    }

    public static void PrepareTransition(GamePlayData data, SceneTransitionContext context)
    {
        if (data == null)
            return;

        data.pendingTransition = context;
    }

    public static SceneTransitionContext PeekPendingTransition(GamePlayData data)
    {
        return data != null ? data.pendingTransition : null;
    }

    public static SceneTransitionContext ConsumePendingTransition(GamePlayData data)
    {
        if (data == null)
            return null;

        var ctx = data.pendingTransition;
        data.pendingTransition = null;
        return ctx;
    }

    public static void PreparePlayerState(GamePlayData data, PlayerRuntimeState state)
    {
        if (data == null)
            return;

        data.pendingPlayerState = state;
    }

    public static void ClearPendingPlayerState(GamePlayData data)
    {
        if (data == null)
            return;

        data.pendingPlayerState = null;
    }

    /// <summary>
    /// 책임 : 런 종료 후 Hub에 도착했을 때 1회성 풀 회복 요청을 소비한다.
    /// 씬 복원/초기화 정책과 분리해 Hub 복귀 보상만 명확하게 처리한다.
    /// </summary>
    public static bool ConsumePendingHubReturnFullHeal(GamePlayData data)
    {
        if (data == null || !data.pendingHubReturnFullHeal)
            return false;

        data.pendingHubReturnFullHeal = false;
        return true;
    }

    /// <summary>
    /// 책임 : 타이틀에서 세이브 프로필을 통해 Hub로 들어온 플레이어의 1회성 풀 회복 요청을 관리한다.
    /// 런 복귀 회복과 분리해 저장 데이터 로드 흐름에서만 명시적으로 소비되도록 한다.
    /// </summary>
    public static void RequestPendingHubLoadFullHeal(GamePlayData data)
    {
        if (data == null)
            return;

        data.pendingHubLoadFullHeal = true;
    }

    public static bool ConsumePendingHubLoadFullHeal(GamePlayData data)
    {
        if (data == null || !data.pendingHubLoadFullHeal)
            return false;

        data.pendingHubLoadFullHeal = false;
        return true;
    }

    public static PlayerRuntimeState PeekPendingPlayerState(GamePlayData data)
    {
        return data != null ? data.pendingPlayerState : null;
    }

    public static PlayerRuntimeState ConsumePendingPlayerState(GamePlayData data)
    {
        if (data == null)
            return null;

        var state = data.pendingPlayerState;
        data.pendingPlayerState = null;
        return state;
    }

    public static void ResetForDevelopmentStart(GamePlayData data, Action clearPendingRunProgress)
    {
        if (data == null)
            return;

        data.isRunActive = false;
        data.runElapsedSeconds = 0f;
        data.runRemainingSeconds = 0f;
        data.lastRunEndReason = RunEndReason.None;
        data.pendingTransition = null;
        data.pendingPlayerState = null;
        data.pendingHubReturnFullHeal = false;
        data.pendingHubLoadFullHeal = false;
        clearPendingRunProgress?.Invoke();
    }
}
