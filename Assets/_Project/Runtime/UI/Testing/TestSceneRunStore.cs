using System;
using System.Collections.Generic;

/// <summary>
/// Keeps the existing volatile run APIs while intercepting terminal commits. Owned
/// by TestSceneControlPanel; no manager, singleton or bootstrap is introduced.
/// </summary>
internal sealed class TestSceneRunStore : IRunSessionStoreBackend
{
    private readonly IRunSessionStoreBackend source;
    public GamePlayData Data => source.Data;
    public event Action OnRunStarted { add => source.OnRunStarted += value; remove => source.OnRunStarted -= value; }
    public event Action<RunEndReason> OnRunEnded;

    public TestSceneRunStore(IRunSessionStoreBackend source) { this.source = source; }
    public void StartRun() => source.StartRun();

    public void EndRun(RunEndReason reason)
    {
        source.ResetForDevelopmentStart();
        Data.lastRunEndReason = reason;
        OnRunEnded?.Invoke(reason);
    }

    public void SetRunRemainingSeconds(float seconds) => source.SetRunRemainingSeconds(seconds);
    public float GetRunRemainingSeconds() => source.GetRunRemainingSeconds();
    public int GetPendingRunMagicStoneDelta() => source.GetPendingRunMagicStoneDelta();
    public void AddPendingRunMagicStoneDelta(int delta) => source.AddPendingRunMagicStoneDelta(delta);
    public void AddPendingAffectionDelta(int npcId, int delta) => source.AddPendingAffectionDelta(npcId, delta);
    public bool TryConsumeAffectionGain(int npcId) => source.TryConsumeAffectionGain(npcId);
    public void AddPendingShortcutUnlock(string mapID, string doorID) => source.AddPendingShortcutUnlock(mapID, doorID);
    public bool HasPendingShortcutUnlock(string mapID, string doorID) => source.HasPendingShortcutUnlock(mapID, doorID);
    public void AddPendingRunSpecialNpcConstructionStart(string id, int clears) => source.AddPendingRunSpecialNpcConstructionStart(id, clears);
    public bool TryGetPendingRunSpecialNpcConstructionStart(string id, out int clears) => source.TryGetPendingRunSpecialNpcConstructionStart(id, out clears);
    public void TickRunTimer(float deltaTime) => source.TickRunTimer(deltaTime);
    public void PrepareTransition(SceneTransitionContext context) => source.PrepareTransition(context);
    public SceneTransitionContext PeekPendingTransition() => source.PeekPendingTransition();
    public SceneTransitionContext ConsumePendingTransition() => source.ConsumePendingTransition();
    public void PreparePlayerState(PlayerRuntimeState state) => source.PreparePlayerState(state);
    public void ClearPendingPlayerState() => source.ClearPendingPlayerState();
    public bool ConsumePendingHubReturnFullHeal() => source.ConsumePendingHubReturnFullHeal();
    public void RequestPendingHubLoadFullHeal() => source.RequestPendingHubLoadFullHeal();
    public bool ConsumePendingHubLoadFullHeal() => source.ConsumePendingHubLoadFullHeal();
    public PlayerRuntimeState PeekPendingPlayerState() => source.PeekPendingPlayerState();
    public PlayerRuntimeState ConsumePendingPlayerState() => source.ConsumePendingPlayerState();
    public int ResolveDungeonSeed(string id, DungeonReentryPolicy policy, int seed) => source.ResolveDungeonSeed(id, policy, seed);
    public bool TryGetDungeonObjectStates(string id, List<DungeonObjectRuntimeStateData> results) => source.TryGetDungeonObjectStates(id, results);
    public void SaveDungeonObjectStates(string id, IReadOnlyList<DungeonObjectRuntimeStateData> states) => source.SaveDungeonObjectStates(id, states);
    public bool TryGetDungeonMapDiscovery(string id, List<int> visited, List<int> revealed) => source.TryGetDungeonMapDiscovery(id, visited, revealed);
    public void SaveDungeonMapDiscovery(string id, IReadOnlyList<int> visited, IReadOnlyList<int> revealed) => source.SaveDungeonMapDiscovery(id, visited, revealed);
    public void ResetForDevelopmentStart() => source.ResetForDevelopmentStart();
}
