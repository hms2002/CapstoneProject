using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Selects reachable opposite-wall portal cells deterministically, independently of Unity scene objects and room selection.</summary>
public static class DungeonReturnPortalPlacement
{
    private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    public static Vector2Int Direction(RoomSocketDirection direction) => Steps[(int)direction];
    public static RoomSocketDirection Opposite(RoomSocketDirection direction) => (RoomSocketDirection)(((int)direction + 2) % 4);

    public static bool TryGetOnlyConnection(DungeonLayoutResult layout, int roomId, out int socketIndex)
    {
        socketIndex = -1;
        int count = 0;
        foreach (DungeonSocketConnection connection in layout.Connections)
        {
            if (connection.FirstRoomPlacementId == roomId) { socketIndex = connection.FirstSocketIndex; count++; }
            if (connection.SecondRoomPlacementId == roomId) { socketIndex = connection.SecondSocketIndex; count++; }
        }
        return count == 1;
    }

    public static List<Vector2Int> Reachable(RectInt bounds, Vector2Int entrance, Func<Vector2Int, bool> walkable)
    {
        var result = new List<Vector2Int>();
        var seen = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        if (!bounds.Contains(entrance) || !walkable(entrance)) return result;
        seen.Add(entrance);
        queue.Enqueue(entrance);
        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            result.Add(cell);
            foreach (Vector2Int step in Steps)
            {
                Vector2Int next = cell + step;
                if (bounds.Contains(next) && seen.Add(next) && walkable(next)) queue.Enqueue(next);
            }
        }
        return result;
    }

    public static bool TryChooseOppositeWall(IReadOnlyList<Vector2Int> reachable, Vector2Int entrance,
        RoomSocketDirection entranceDirection, Func<Vector2Int, bool> isWall,
        Func<Vector2Int, bool> canPlace, out Vector2Int chosen)
    {
        chosen = default;
        Vector2Int inward = -Direction(entranceDirection);
        Vector2Int tangent = new(-inward.y, inward.x);
        float bestScore = float.NegativeInfinity;
        foreach (Vector2Int cell in reachable)
        {
            Vector2Int delta = cell - entrance;
            float depth = Vector2.Dot(delta, inward);
            if (depth <= 0f || !isWall(cell + inward) || !canPlace(cell)) continue;
            // Prefer the opposite boundary, then the closest alignment with the entrance.
            float score = depth * 10000f - Mathf.Abs(Vector2.Dot(delta, tangent));
            if (score <= bestScore) continue;
            bestScore = score;
            chosen = cell;
        }
        return bestScore > float.NegativeInfinity;
    }
}
