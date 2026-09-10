using System;
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
/// 책임 : 절차 생성 방 하나를 미니맵에 투영하는 안정 배치 Id, 역할과 실제 점유 영역을 보관한다.
/// </summary>
public readonly struct DungeonMapRoomNode
{
    public int PlacementId { get; }
    public RoomType RoomType { get; }
    public Rect WorldBounds { get; }
    public Vector2 WorldCenter => WorldBounds.center;
    public Vector2Int ShapeGridSize { get; }
    public IReadOnlyList<RectInt> ShapeRectangles { get; }
    public Vector2 IconAnchor { get; }

    public DungeonMapRoomNode(int placementId, RoomType roomType, Vector2 worldCenter)
        : this(
            placementId,
            roomType,
            new Rect(worldCenter - Vector2.one * 0.5f, Vector2.one))
    {
    }

    public DungeonMapRoomNode(int placementId, RoomType roomType, Rect worldBounds)
        : this(
            placementId,
            roomType,
            worldBounds,
            new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(worldBounds.width)),
                Mathf.Max(1, Mathf.RoundToInt(worldBounds.height))),
            Array.Empty<RectInt>())
    {
    }

    public DungeonMapRoomNode(
        int placementId,
        RoomType roomType,
        Rect worldBounds,
        Vector2Int shapeGridSize,
        IReadOnlyList<RectInt> shapeRectangles,
        Vector2? iconAnchor = null)
    {
        PlacementId = placementId;
        RoomType = roomType;
        WorldBounds = worldBounds;
        ShapeGridSize = new Vector2Int(
            Mathf.Max(1, shapeGridSize.x),
            Mathf.Max(1, shapeGridSize.y));
        ShapeRectangles = shapeRectangles ?? Array.Empty<RectInt>();
        IconAnchor = iconAnchor ?? DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(
            ShapeRectangles, ShapeGridSize);
    }
}

/// <summary>
/// 책임 : 미니맵 노드 두 개의 연결 관계와 실제 복도가 시작·종료되는 양쪽 출입구 위치를 표현한다.
/// </summary>
public readonly struct DungeonMapConnection
{
    public int FirstRoomPlacementId { get; }
    public int SecondRoomPlacementId { get; }
    public Vector2 FirstWorldSocketCenter { get; }
    public Vector2 SecondWorldSocketCenter { get; }
    public bool HasSocketEndpoints { get; }

    public DungeonMapConnection(int firstRoomPlacementId, int secondRoomPlacementId)
    {
        FirstRoomPlacementId = firstRoomPlacementId;
        SecondRoomPlacementId = secondRoomPlacementId;
        FirstWorldSocketCenter = Vector2.zero;
        SecondWorldSocketCenter = Vector2.zero;
        HasSocketEndpoints = false;
    }

    public DungeonMapConnection(
        int firstRoomPlacementId,
        int secondRoomPlacementId,
        Vector2 firstWorldSocketCenter,
        Vector2 secondWorldSocketCenter)
    {
        FirstRoomPlacementId = firstRoomPlacementId;
        SecondRoomPlacementId = secondRoomPlacementId;
        FirstWorldSocketCenter = firstWorldSocketCenter;
        SecondWorldSocketCenter = secondWorldSocketCenter;
        HasSocketEndpoints = true;
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
        var shapesByTemplate = new Dictionary<RoomTemplateSO, RectInt[]>();
        var anchorsByTemplate = new Dictionary<RoomTemplateSO, Vector2>();
        if (layout == null)
            return new DungeonMapGraphSnapshot(roomNodes, roomConnections);

        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement placement = layout.Rooms[roomIndex];
            if (placement?.Template == null)
                continue;

            RectInt localBounds = ResolveLocalBounds(placement.Template.LayoutData);
            if (!shapesByTemplate.TryGetValue(
                    placement.Template,
                    out RectInt[] shapeRectangles))
            {
                shapeRectangles = DungeonMapRoomShapeBuilder.Build(
                    placement.Template.BuildData,
                    localBounds);
                shapesByTemplate[placement.Template] = shapeRectangles;
                anchorsByTemplate[placement.Template] =
                    DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(shapeRectangles, localBounds.size);
            }

            roomNodes.Add(new DungeonMapRoomNode(
                placement.PlacementId,
                placement.Template.LayoutData.roomType,
                new Rect(
                    placement.WorldBounds.xMin,
                    placement.WorldBounds.yMin,
                    placement.WorldBounds.width,
                    placement.WorldBounds.height),
                localBounds.size,
                shapeRectangles,
                anchorsByTemplate[placement.Template]));
        }

        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            DungeonSocketConnection connection = layout.Connections[connectionIndex];
            DungeonRoomPlacement firstRoom = layout.GetRoom(
                connection.FirstRoomPlacementId);
            DungeonRoomPlacement secondRoom = layout.GetRoom(
                connection.SecondRoomPlacementId);
            if (TryResolveSocketBoundaryCenter(
                    firstRoom,
                    connection.FirstSocketIndex,
                    out Vector2 firstSocketCenter) &&
                TryResolveSocketBoundaryCenter(
                    secondRoom,
                    connection.SecondSocketIndex,
                    out Vector2 secondSocketCenter))
            {
                roomConnections.Add(new DungeonMapConnection(
                    connection.FirstRoomPlacementId,
                    connection.SecondRoomPlacementId,
                    firstSocketCenter,
                    secondSocketCenter));
            }
            else
            {
                roomConnections.Add(new DungeonMapConnection(
                    connection.FirstRoomPlacementId,
                    connection.SecondRoomPlacementId));
            }
        }

        return new DungeonMapGraphSnapshot(roomNodes, roomConnections);
    }

    private static bool TryResolveSocketBoundaryCenter(
        DungeonRoomPlacement placement,
        int socketIndex,
        out Vector2 socketCenter)
    {
        socketCenter = Vector2.zero;
        if (placement?.Template == null)
            return false;

        IReadOnlyList<RoomSocketData> sockets = placement.Template.LayoutData.sockets;
        if (sockets == null || socketIndex < 0 || socketIndex >= sockets.Count)
            return false;

        RoomSocketData socket = sockets[socketIndex];
        int width = RoomSocketGeometry.ResolveWidth(socket);
        Vector2 tangent = RoomSocketGeometry.GetTangent(socket.direction);
        Vector2 outward = DirectionToVector(socket.direction);
        socketCenter = placement.Origin +
                       socket.localCell +
                       Vector2.one * 0.5f +
                       tangent * ((width - 1) * 0.5f) +
                       outward * 0.5f;
        return true;
    }

    private static Vector2 DirectionToVector(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => Vector2.up,
            RoomSocketDirection.Right => Vector2.right,
            RoomSocketDirection.Down => Vector2.down,
            RoomSocketDirection.Left => Vector2.left,
            _ => Vector2.zero
        };
    }

    private static RectInt ResolveLocalBounds(RoomLayoutData layout)
    {
        if (layout.localBounds.width > 0 && layout.localBounds.height > 0)
            return layout.localBounds;

        return new RectInt(Vector2Int.zero, layout.size);
    }
}

/// <summary>
/// 책임 : 방의 타일 합집합을 직사각형 메시로 압축하고, 경계에서 여유가 있는 내부 표시 기준점을 계산한다.
/// </summary>
public static class DungeonMapRoomShapeBuilder
{
    public static Vector2 ResolveInteriorAnchor(
        IReadOnlyList<RectInt> rectangles,
        Vector2Int gridSize)
    {
        Vector2 fallback = new(0.5f, 0.5f);
        if (rectangles == null || rectangles.Count == 0 || gridSize.x <= 0 || gridSize.y <= 0)
            return fallback;

        int width = gridSize.x;
        int height = gridSize.y;
        int stride = width + 2;
        int[] clearance = new int[stride * (height + 2)];
        int unvisited = Mathf.Max(width, height) + 1;
        for (int r = 0; r < rectangles.Count; r++)
        {
            RectInt rect = rectangles[r];
            for (int y = Mathf.Max(0, rect.yMin); y < Mathf.Min(height, rect.yMax); y++)
            for (int x = Mathf.Max(0, rect.xMin); x < Mathf.Min(width, rect.xMax); x++)
                clearance[(y + 1) * stride + x + 1] = unvisited;
        }

        // A padded, eight-neighbor distance transform measures square icon clearance.
        // Rectangle seams are occupied, not boundaries. Both passes cost O(width * height).
        for (int y = 1; y <= height; y++)
        for (int x = 1; x <= width; x++)
        {
            int i = y * stride + x;
            if (clearance[i] == 0)
                continue;
            int neighbor = Mathf.Min(clearance[i - 1], clearance[i - stride]);
            neighbor = Mathf.Min(neighbor, Mathf.Min(clearance[i - stride - 1], clearance[i - stride + 1]));
            clearance[i] = Mathf.Min(clearance[i], neighbor + 1);
        }

        for (int y = height; y >= 1; y--)
        for (int x = width; x >= 1; x--)
        {
            int i = y * stride + x;
            if (clearance[i] == 0)
                continue;
            int neighbor = Mathf.Min(clearance[i + 1], clearance[i + stride]);
            neighbor = Mathf.Min(neighbor, Mathf.Min(clearance[i + stride - 1], clearance[i + stride + 1]));
            clearance[i] = Mathf.Min(clearance[i], neighbor + 1);
        }

        Vector2 center = (Vector2)gridSize * 0.5f;
        Vector2 selected = center;
        int bestClearance = 0;
        float bestDistance = float.MaxValue;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int distanceToEmpty = clearance[(y + 1) * stride + x + 1];
            if (distanceToEmpty == 0 || distanceToEmpty < bestClearance)
                continue;
            Vector2 cell = new(x + 0.5f, y + 0.5f);
            float distanceToCenter = (cell - center).sqrMagnitude;
            if (distanceToEmpty == bestClearance && distanceToCenter >= bestDistance)
                continue;
            bestClearance = distanceToEmpty;
            bestDistance = distanceToCenter;
            selected = cell;
        }

        return new Vector2(selected.x / width, selected.y / height);
    }

    public static RectInt[] Build(RoomBuildData buildData, RectInt localBounds)
    {
        if (localBounds.width <= 0 || localBounds.height <= 0)
            return Array.Empty<RectInt>();

        var occupiedCells = new HashSet<Vector2Int>();
        AddOccupiedCells(buildData.floorTiles, localBounds, occupiedCells);
        AddOccupiedCells(buildData.wallTiles, localBounds, occupiedCells);
        if (occupiedCells.Count == 0)
            return Array.Empty<RectInt>();

        var rectangles = new List<RectInt>();
        var previousRuns = new Dictionary<Vector2Int, int>();
        for (int localY = 0; localY < localBounds.height; localY++)
        {
            int worldY = localBounds.yMin + localY;
            var currentRuns = new Dictionary<Vector2Int, int>();
            int localX = 0;
            while (localX < localBounds.width)
            {
                int worldX = localBounds.xMin + localX;
                if (!occupiedCells.Contains(new Vector2Int(worldX, worldY)))
                {
                    localX++;
                    continue;
                }

                int runStart = localX;
                do
                {
                    localX++;
                    worldX = localBounds.xMin + localX;
                }
                while (localX < localBounds.width &&
                       occupiedCells.Contains(new Vector2Int(worldX, worldY)));

                int runWidth = localX - runStart;
                Vector2Int runKey = new(runStart, runWidth);
                if (previousRuns.TryGetValue(runKey, out int rectangleIndex) &&
                    rectangles[rectangleIndex].yMax == localY)
                {
                    RectInt rectangle = rectangles[rectangleIndex];
                    rectangle.height++;
                    rectangles[rectangleIndex] = rectangle;
                    currentRuns[runKey] = rectangleIndex;
                    continue;
                }

                currentRuns[runKey] = rectangles.Count;
                rectangles.Add(new RectInt(runStart, localY, runWidth, 1));
            }

            previousRuns = currentRuns;
        }

        return rectangles.ToArray();
    }

    private static void AddOccupiedCells(
        IReadOnlyList<RoomTileData> tiles,
        RectInt localBounds,
        HashSet<Vector2Int> occupiedCells)
    {
        if (tiles == null)
            return;

        for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
        {
            RoomTileData tileData = tiles[tileIndex];
            if (tileData.tile != null && localBounds.Contains(tileData.localCell))
                occupiedCells.Add(tileData.localCell);
        }
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
