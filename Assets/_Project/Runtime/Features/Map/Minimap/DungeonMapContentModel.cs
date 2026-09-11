using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Responsibility: identify live contents independently of room roles and UI sprites.</summary>
public enum DungeonMapContentKind { ClosedChest = 0, OpenedChest = 1, Heart = 2 }

/// <summary>Responsibility: expose an immutable per-room count snapshot to map presentation.</summary>
public readonly struct DungeonMapRoomContents
{
    public int ClosedChestCount { get; }
    public int OpenedChestCount { get; }
    public int HeartCount { get; }

    public DungeonMapRoomContents(int closedChests, int openedChests, int hearts)
    {
        ClosedChestCount = closedChests;
        OpenedChestCount = openedChests;
        HeartCount = hearts;
    }

    public int GetCount(DungeonMapContentKind kind) => kind switch
    {
        DungeonMapContentKind.ClosedChest => ClosedChestCount,
        DungeonMapContentKind.OpenedChest => OpenedChestCount,
        DungeonMapContentKind.Heart => HeartCount,
        _ => 0
    };
}

/// <summary>
/// Responsibility: aggregate source identities into room counts without owning loot or discovery.
/// Repeated notifications are idempotent; state replacement is atomic before notification.
/// </summary>
public sealed class DungeonMapContentModel
{
    /// <summary>Responsibility: retain one source's room and current content kind.</summary>
    private readonly struct Entry
    {
        public readonly int RoomId;
        public readonly DungeonMapContentKind Kind;
        public Entry(int roomId, DungeonMapContentKind kind) { RoomId = roomId; Kind = kind; }
    }

    private readonly Dictionary<int, Entry> sources = new();
    private readonly Dictionary<int, DungeonMapRoomContents> rooms = new();
    public event Action<int> Changed;
    public DungeonMapRoomContents GetContents(int roomId) =>
        rooms.TryGetValue(roomId, out var counts) ? counts : default;

    public void Set(int sourceId, int roomId, DungeonMapContentKind kind)
    {
        if (roomId < 0)
        {
            Remove(sourceId);
            return;
        }
        bool hadPrevious = sources.TryGetValue(sourceId, out Entry previous);
        if (hadPrevious && previous.RoomId == roomId && previous.Kind == kind)
            return;
        if (hadPrevious)
            Adjust(previous.RoomId, previous.Kind, -1);
        sources[sourceId] = new Entry(roomId, kind);
        Adjust(roomId, kind, 1);
        if (hadPrevious && previous.RoomId != roomId)
            Changed?.Invoke(previous.RoomId);
        Changed?.Invoke(roomId);
    }

    public void Remove(int sourceId)
    {
        if (!sources.TryGetValue(sourceId, out Entry previous))
            return;
        sources.Remove(sourceId);
        Adjust(previous.RoomId, previous.Kind, -1);
        Changed?.Invoke(previous.RoomId);
    }

    private void Adjust(int roomId, DungeonMapContentKind kind, int delta)
    {
        DungeonMapRoomContents before = GetContents(roomId);
        var after = new DungeonMapRoomContents(
            before.ClosedChestCount + (kind == DungeonMapContentKind.ClosedChest ? delta : 0),
            before.OpenedChestCount + (kind == DungeonMapContentKind.OpenedChest ? delta : 0),
            before.HeartCount + (kind == DungeonMapContentKind.Heart ? delta : 0));
        if (after.ClosedChestCount + after.OpenedChestCount + after.HeartCount == 0)
            rooms.Remove(roomId);
        else
            rooms[roomId] = after;
    }
}

/// <summary>
/// Responsibility: assign contents to occupied room shapes, not just bounding boxes.
/// Convert scene world positions to the builder's cell space before comparing layout bounds.
/// Contents in corridors or just outside a wall are grouped with the nearest room.
/// </summary>
public static class DungeonMapContentRoomResolver
{
    public static int Resolve(DungeonMapGraphSnapshot graph, Vector3 worldPosition, GridLayout layoutGrid)
    {
        Vector3 layoutPosition = layoutGrid != null
            ? layoutGrid.LocalToCellInterpolated(layoutGrid.WorldToLocal(worldPosition))
            : worldPosition;
        return Resolve(graph, (Vector2)layoutPosition);
    }

    public static int Resolve(DungeonMapGraphSnapshot graph, Vector2 position)
    {
        if (graph == null)
            return -1;
        int result = -1;
        float bestDistance = float.PositiveInfinity;
        foreach (DungeonMapRoomNode room in graph.Rooms)
        {
            float distance = float.PositiveInfinity;
            if (room.ShapeRectangles.Count == 0)
                distance = DistanceSquared(position, room.WorldBounds);
            else
            {
                Vector2 scale = room.WorldBounds.size / (Vector2)room.ShapeGridSize;
                foreach (RectInt shape in room.ShapeRectangles)
                {
                    var rect = new Rect(room.WorldBounds.min + (Vector2)shape.min * scale,
                        (Vector2)shape.size * scale);
                    distance = Mathf.Min(distance, DistanceSquared(position, rect));
                }
            }
            if (distance < bestDistance || (distance == bestDistance && room.PlacementId < result))
            {
                bestDistance = distance;
                result = room.PlacementId;
            }
        }
        return result;
    }

    private static float DistanceSquared(Vector2 point, Rect rect)
    {
        Vector2 nearest = new(Mathf.Clamp(point.x, rect.xMin, rect.xMax),
            Mathf.Clamp(point.y, rect.yMin, rect.yMax));
        return (point - nearest).sqrMagnitude;
    }
}
