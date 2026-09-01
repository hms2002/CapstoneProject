using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 현재 절차 던전의 미니맵 그래프와 발견 상태 수명을 소유하고, 방 진입 및 런 저장소 변경을 UI 이벤트로 중계한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonMapRuntimeController : MonoBehaviour
{
    private readonly List<int> visitedStateBuffer = new();
    private readonly List<int> revealedStateBuffer = new();

    private DungeonMapGraphSnapshot graph;
    private DungeonMapDiscoveryModel discovery;
    private string dungeonStateId;
    private DungeonReentryPolicy reentryPolicy;
    private bool configured;

    public static DungeonMapRuntimeController Active { get; private set; }
    public static event Action<DungeonMapRuntimeController> ActiveChanged;

    public event Action Changed;

    public DungeonMapGraphSnapshot Graph => graph;
    public int CurrentRoomPlacementId => discovery?.CurrentRoomPlacementId ?? -1;
    public bool IsConfigured => configured;

    public void Configure(
        DungeonLayoutResult layout,
        string stateId,
        DungeonReentryPolicy policy)
    {
        graph = DungeonMapGraphSnapshot.Create(layout);
        discovery = new DungeonMapDiscoveryModel(graph);
        dungeonStateId = stateId ?? string.Empty;
        reentryPolicy = policy;
        configured = graph.Rooms.Count > 0;

        bool restored = false;
        if (configured && ShouldPersistDiscovery())
        {
            restored = RunSessionStore.TryGetDungeonMapDiscovery(
                dungeonStateId,
                visitedStateBuffer,
                revealedStateBuffer);
            if (restored)
                discovery.Restore(visitedStateBuffer, revealedStateBuffer);
        }

        if (configured && !restored)
            discovery.RevealInitialStartRoom();

        SetActiveRuntime(configured ? this : null);
        if (configured)
        {
            CaptureDiscoveryState();
            Changed?.Invoke();
        }
    }

    public void ClearConfiguration()
    {
        configured = false;
        graph = null;
        discovery = null;
        dungeonStateId = string.Empty;
        visitedStateBuffer.Clear();
        revealedStateBuffer.Clear();

        if (Active == this)
            SetActiveRuntime(null);
    }

    public void NotifyPlayerEnteredRoom(int roomPlacementId)
    {
        if (!configured || discovery == null || !discovery.EnterRoom(roomPlacementId))
            return;

        CaptureDiscoveryState();
        Changed?.Invoke();
    }

    public DungeonMapRoomVisibility GetVisibility(int roomPlacementId)
    {
        return discovery != null
            ? discovery.GetVisibility(roomPlacementId)
            : DungeonMapRoomVisibility.Unknown;
    }

    public void CaptureDiscoveryState()
    {
        if (!configured || discovery == null || !ShouldPersistDiscovery())
            return;

        CopySorted(discovery.VisitedRoomPlacementIds, visitedStateBuffer);
        CopySorted(discovery.RevealedRoomPlacementIds, revealedStateBuffer);
        RunSessionStore.SaveDungeonMapDiscovery(
            dungeonStateId,
            visitedStateBuffer,
            revealedStateBuffer);
    }

    private void OnDisable()
    {
        CaptureDiscoveryState();
        if (Active == this)
            SetActiveRuntime(null);
    }

    private void OnDestroy()
    {
        if (Active == this)
            SetActiveRuntime(null);
    }

    private bool ShouldPersistDiscovery()
    {
        return reentryPolicy == DungeonReentryPolicy.PreserveDuringRun &&
               RunSessionStore.IsRunActive &&
               !string.IsNullOrWhiteSpace(dungeonStateId);
    }

    private static void CopySorted(IReadOnlyCollection<int> source, List<int> destination)
    {
        destination.Clear();
        if (source == null)
            return;

        foreach (int placementId in source)
            destination.Add(placementId);

        destination.Sort();
    }

    private static void SetActiveRuntime(DungeonMapRuntimeController runtime)
    {
        if (Active == runtime)
            return;

        Active = runtime;
        ActiveChanged?.Invoke(runtime);
    }
}
