using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임: 런 시작과 종료 경계에서 저장 데이터, 진행 상태, 후속 저장 동작을 일관되게 정리한다.
/// </summary>
internal static class RunSessionLifecycleService
{
    public static void StartRun(GamePlayData data, Action clearPendingRunProgress)
    {
        if (data == null)
            return;

        clearPendingRunProgress?.Invoke();
        RunSessionStateService.ResetLevelProgression(data);
        data.isRunActive = true;
        data.runElapsedSeconds = 0f;
        data.runRemainingSeconds = 0f;
        data.pendingHubReturnFullHeal = false;
        data.pendingHubLoadFullHeal = false;
        data.defeatedBossIds ??= new List<string>();
        data.defeatedBossIds.Clear();
        data.dungeonRunStates ??= new List<DungeonRunStateData>();
        data.dungeonRunStates.Clear();
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
        data.defeatedBossIds ??= new List<string>();
        data.defeatedBossIds.Clear();
        data.dungeonRunStates ??= new List<DungeonRunStateData>();
        data.dungeonRunStates.Clear();
        RunSessionStateService.ResetLevelProgression(data);
        clearRoutePlan?.Invoke();
    }
}

/// <summary>
/// 책임: 한 런에 귀속되는 보스, 던전, 플레이어 전환 및 레벨 진행 상태를 조회하고 변경한다.
/// </summary>
internal static class RunSessionStateService
{
    public static bool HasDefeatedBoss(GamePlayData data, string bossId)
    {
        if (data?.defeatedBossIds == null || string.IsNullOrWhiteSpace(bossId))
            return false;

        return data.defeatedBossIds.Exists(
            candidate => string.Equals(candidate, bossId, StringComparison.Ordinal));
    }

    public static void MarkBossDefeated(GamePlayData data, string bossId)
    {
        if (data == null || string.IsNullOrWhiteSpace(bossId))
            return;

        data.defeatedBossIds ??= new List<string>();
        if (!HasDefeatedBoss(data, bossId))
            data.defeatedBossIds.Add(bossId);
    }

    public static int ResolveDungeonSeed(
        GamePlayData data,
        string dungeonId,
        DungeonReentryPolicy policy,
        int fallbackSeed)
    {
        if (data == null || !data.isRunActive || string.IsNullOrWhiteSpace(dungeonId))
            return fallbackSeed;

        data.dungeonRunStates ??= new List<DungeonRunStateData>();
        DungeonRunStateData state = FindDungeonState(data, dungeonId);

        if (policy == DungeonReentryPolicy.RegenerateOnEntry)
        {
            if (state != null)
                data.dungeonRunStates.Remove(state);

            return Guid.NewGuid().GetHashCode();
        }

        state ??= CreateDungeonState(data, dungeonId);
        if (!state.hasResolvedSeed)
        {
            state.resolvedSeed = Guid.NewGuid().GetHashCode();
            state.hasResolvedSeed = true;
        }

        if (policy == DungeonReentryPolicy.ResetContentsKeepLayout)
        {
            state.objectStates ??= new List<DungeonObjectRuntimeStateData>();
            state.objectStates.Clear();
        }

        return state.resolvedSeed;
    }

    public static bool TryGetDungeonObjectStates(
        GamePlayData data,
        string dungeonId,
        List<DungeonObjectRuntimeStateData> destination)
    {
        if (destination == null)
            return false;

        destination.Clear();
        DungeonRunStateData state = FindDungeonState(data, dungeonId);
        if (state?.objectStates == null || state.objectStates.Count == 0)
            return false;

        for (int i = 0; i < state.objectStates.Count; i++)
        {
            DungeonObjectRuntimeStateData source = state.objectStates[i];
            if (source == null)
                continue;

            destination.Add(CloneDungeonObjectState(source));
        }

        return destination.Count > 0;
    }

    public static void SaveDungeonObjectStates(
        GamePlayData data,
        string dungeonId,
        IReadOnlyList<DungeonObjectRuntimeStateData> states)
    {
        if (data == null || !data.isRunActive || string.IsNullOrWhiteSpace(dungeonId))
            return;

        data.dungeonRunStates ??= new List<DungeonRunStateData>();
        DungeonRunStateData state = FindDungeonState(data, dungeonId) ??
                                    CreateDungeonState(data, dungeonId);
        state.objectStates ??= new List<DungeonObjectRuntimeStateData>();
        state.objectStates.Clear();

        if (states == null)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            DungeonObjectRuntimeStateData source = states[i];
            if (source == null || string.IsNullOrWhiteSpace(source.stateId))
                continue;

            state.objectStates.Add(CloneDungeonObjectState(source));
        }
    }

    private static DungeonObjectRuntimeStateData CloneDungeonObjectState(
        DungeonObjectRuntimeStateData source)
    {
        var clone = new DungeonObjectRuntimeStateData
        {
            stateId = source.stateId,
            isPresent = source.isPresent,
            isActive = source.isActive,
            isChestOpened = source.isChestOpened,
            chestLoot = new List<DungeonChestLootRuntimeStateData>()
        };

        if (source.chestLoot == null)
            return clone;

        for (int i = 0; i < source.chestLoot.Count; i++)
        {
            DungeonChestLootRuntimeStateData loot = source.chestLoot[i];
            if (loot == null)
                continue;

            clone.chestLoot.Add(new DungeonChestLootRuntimeStateData
            {
                slotIndex = loot.slotIndex,
                item = loot.item,
                relicLevel = loot.relicLevel
            });
        }

        return clone;
    }

    private static DungeonRunStateData FindDungeonState(GamePlayData data, string dungeonId)
    {
        if (data?.dungeonRunStates == null || string.IsNullOrWhiteSpace(dungeonId))
            return null;

        return data.dungeonRunStates.Find(
            candidate => candidate != null &&
                         string.Equals(candidate.dungeonId, dungeonId, StringComparison.Ordinal));
    }

    private static DungeonRunStateData CreateDungeonState(GamePlayData data, string dungeonId)
    {
        var state = new DungeonRunStateData { dungeonId = dungeonId };
        data.dungeonRunStates.Add(state);
        return state;
    }

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
        data.defeatedBossIds ??= new List<string>();
        data.defeatedBossIds.Clear();
        data.dungeonRunStates ??= new List<DungeonRunStateData>();
        data.dungeonRunStates.Clear();
        ResetLevelProgression(data);
        clearPendingRunProgress?.Invoke();
    }

    public static void ResetLevelProgression(GamePlayData data)
    {
        data.levelProgression ??= new LevelProgressionState();
        data.levelProgression.Reset();
    }
}
