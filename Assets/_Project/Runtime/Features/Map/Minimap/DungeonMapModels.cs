using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 미니맵에서 방이 아직 숨겨졌는지, 인접 공개됐는지, 직접 방문됐는지를 표현한다.
/// </summary>
public enum DungeonMapRoomVisibility
{
    Unknown = 0,
    Revealed = 1,
    Visited = 2
}

/// <summary>
/// 책임 : 절차 생성 방 하나를 미니맵에 투영하는 안정 배치 Id, 역할과 실제 배치 중심점을 보관한다.
/// </summary>
public readonly struct DungeonMapRoomNode
{
    public int PlacementId { get; }
    public RoomType RoomType { get; }
    public Vector2 WorldCenter { get; }

    public DungeonMapRoomNode(int placementId, RoomType roomType, Vector2 worldCenter)
    {
        PlacementId = placementId;
        RoomType = roomType;
        WorldCenter = worldCenter;
    }
}

/// <summary>
/// 책임 : 미니맵 노드 두 개가 실제 던전 복도로 연결되어 있음을 배치 Id 쌍으로 표현한다.
/// </summary>
public readonly struct DungeonMapConnection
{
    public int FirstRoomPlacementId { get; }
    public int SecondRoomPlacementId { get; }

    public DungeonMapConnection(int firstRoomPlacementId, int secondRoomPlacementId)
    {
        FirstRoomPlacementId = firstRoomPlacementId;
        SecondRoomPlacementId = secondRoomPlacementId;
    }
}

/// <summary>
/// 책임 : DungeonLayoutResult의 방과 연결을 UI가 생성 알고리즘 세부사항 없이 읽을 수 있는 불변 지도 그래프로 변환한다.
/// </summary>
public sealed class DungeonMapGraphSnapshot
{
    private readonly List<DungeonMapRoomNode> rooms;
    private readonly List<DungeonMapConnection> connections;

    public IReadOnlyList<DungeonMapRoomNode> Rooms => rooms;
    public IReadOnlyList<DungeonMapConnection> Connections => connections;

    public DungeonMapGraphSnapshot(
        IReadOnlyList<DungeonMapRoomNode> roomNodes,
        IReadOnlyList<DungeonMapConnection> roomConnections)
    {
        rooms = roomNodes != null
            ? new List<DungeonMapRoomNode>(roomNodes)
            : new List<DungeonMapRoomNode>();
        connections = roomConnections != null
            ? new List<DungeonMapConnection>(roomConnections)
            : new List<DungeonMapConnection>();
    }

    public static DungeonMapGraphSnapshot Create(DungeonLayoutResult layout)
    {
        var roomNodes = new List<DungeonMapRoomNode>();
        var roomConnections = new List<DungeonMapConnection>();
        if (layout == null)
            return new DungeonMapGraphSnapshot(roomNodes, roomConnections);

        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement placement = layout.Rooms[roomIndex];
            if (placement?.Template == null)
                continue;

            roomNodes.Add(new DungeonMapRoomNode(
                placement.PlacementId,
                placement.Template.LayoutData.roomType,
                placement.WorldBounds.center));
        }

        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            DungeonSocketConnection connection = layout.Connections[connectionIndex];
            roomConnections.Add(new DungeonMapConnection(
                connection.FirstRoomPlacementId,
                connection.SecondRoomPlacementId));
        }

        return new DungeonMapGraphSnapshot(roomNodes, roomConnections);
    }
}

/// <summary>
/// 책임 : 지도 그래프에서 방문 방, 직접 인접 공개 방과 현재 방을 판정하며 두 단계 밖의 방이 노출되지 않게 한다.
/// </summary>
public sealed class DungeonMapDiscoveryModel
{
    private readonly Dictionary<int, DungeonMapRoomNode> roomsByPlacement = new();
    private readonly Dictionary<int, HashSet<int>> neighborsByPlacement = new();
    private readonly HashSet<int> visitedRoomPlacementIds = new();
    private readonly HashSet<int> revealedRoomPlacementIds = new();

    public int CurrentRoomPlacementId { get; private set; } = -1;
    public IReadOnlyCollection<int> VisitedRoomPlacementIds => visitedRoomPlacementIds;
    public IReadOnlyCollection<int> RevealedRoomPlacementIds => revealedRoomPlacementIds;

    public DungeonMapDiscoveryModel(DungeonMapGraphSnapshot graph)
    {
        if (graph == null)
            return;

        for (int roomIndex = 0; roomIndex < graph.Rooms.Count; roomIndex++)
        {
            DungeonMapRoomNode room = graph.Rooms[roomIndex];
            roomsByPlacement[room.PlacementId] = room;
            neighborsByPlacement[room.PlacementId] = new HashSet<int>();
        }

        for (int connectionIndex = 0;
             connectionIndex < graph.Connections.Count;
             connectionIndex++)
        {
            DungeonMapConnection connection = graph.Connections[connectionIndex];
            if (!neighborsByPlacement.ContainsKey(connection.FirstRoomPlacementId) ||
                !neighborsByPlacement.ContainsKey(connection.SecondRoomPlacementId))
            {
                continue;
            }

            neighborsByPlacement[connection.FirstRoomPlacementId]
                .Add(connection.SecondRoomPlacementId);
            neighborsByPlacement[connection.SecondRoomPlacementId]
                .Add(connection.FirstRoomPlacementId);
        }
    }

    public bool RevealInitialStartRoom()
    {
        foreach (KeyValuePair<int, DungeonMapRoomNode> pair in roomsByPlacement)
        {
            if (pair.Value.RoomType == RoomType.Start)
                return DiscoverRoomAndNeighbors(pair.Key, makeCurrent: false);
        }

        return false;
    }

    public bool EnterRoom(int roomPlacementId)
    {
        return DiscoverRoomAndNeighbors(roomPlacementId, makeCurrent: true);
    }

    public void Restore(
        IReadOnlyList<int> visitedIds,
        IReadOnlyList<int> revealedIds)
    {
        visitedRoomPlacementIds.Clear();
        revealedRoomPlacementIds.Clear();
        CurrentRoomPlacementId = -1;

        CopyKnownIds(revealedIds, revealedRoomPlacementIds);
        CopyKnownIds(visitedIds, visitedRoomPlacementIds);

        foreach (int visitedId in visitedRoomPlacementIds)
        {
            revealedRoomPlacementIds.Add(visitedId);
            RevealNeighbors(visitedId);
        }
    }

    public DungeonMapRoomVisibility GetVisibility(int roomPlacementId)
    {
        if (visitedRoomPlacementIds.Contains(roomPlacementId))
            return DungeonMapRoomVisibility.Visited;

        return revealedRoomPlacementIds.Contains(roomPlacementId)
            ? DungeonMapRoomVisibility.Revealed
            : DungeonMapRoomVisibility.Unknown;
    }

    private bool DiscoverRoomAndNeighbors(int roomPlacementId, bool makeCurrent)
    {
        if (!roomsByPlacement.ContainsKey(roomPlacementId))
            return false;

        bool changed = visitedRoomPlacementIds.Add(roomPlacementId);
        changed |= revealedRoomPlacementIds.Add(roomPlacementId);
        changed |= RevealNeighbors(roomPlacementId);

        if (makeCurrent && CurrentRoomPlacementId != roomPlacementId)
        {
            CurrentRoomPlacementId = roomPlacementId;
            changed = true;
        }

        return changed;
    }

    private bool RevealNeighbors(int roomPlacementId)
    {
        if (!neighborsByPlacement.TryGetValue(roomPlacementId, out HashSet<int> neighbors))
            return false;

        bool changed = false;
        foreach (int neighborId in neighbors)
            changed |= revealedRoomPlacementIds.Add(neighborId);

        return changed;
    }

    private void CopyKnownIds(IReadOnlyList<int> source, HashSet<int> destination)
    {
        if (source == null)
            return;

        for (int index = 0; index < source.Count; index++)
        {
            int placementId = source[index];
            if (roomsByPlacement.ContainsKey(placementId))
                destination.Add(placementId);
        }
    }
}
