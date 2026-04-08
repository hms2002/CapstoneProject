using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityGAS;

public sealed class GroundTileDropPositionResolver
{
    private readonly int groundLayer;

    public GroundTileDropPositionResolver()
    {
        int resolvedGroundLayer = LayerMask.NameToLayer("Ground");
        groundLayer = resolvedGroundLayer >= 0 ? resolvedGroundLayer : 7;
    }

    public List<Vector3> GetNearbyGroundPositions(Vector3 origin, int tileRadius = 1)
    {
        return GetHorizontalGroundPositions(origin, tileRadius);
    }

    public List<Vector3> GetHorizontalGroundPositions(Vector3 origin, int horizontalRadius = 1)
    {
        var positions = new List<Vector3>();
        var seenCells = new HashSet<string>();

        foreach (Tilemap map in GetGroundTilemaps())
        {
            if (!TryFindClosestGroundCell(map, origin, horizontalRadius, out Vector3Int anchorCell))
                continue;

            foreach (int x in BuildHorizontalOffsets(horizontalRadius))
            {
                Vector3Int cell = anchorCell + new Vector3Int(x, 0, 0);
                if (!map.HasTile(cell))
                    continue;

                string key = $"{map.GetInstanceID()}:{cell.x}:{cell.y}:{cell.z}";
                if (!seenCells.Add(key))
                    continue;

                positions.Add(GetCellCenterWorld(map, cell, origin.z));
            }
        }

        return positions;
    }

    public bool TryResolveForwardGroundPosition(Vector3 origin, Transform directionSource, out Vector3 landingPosition)
    {
        Vector2 direction = directionSource != null
            ? AbilityAimResolver2D.Resolve(directionSource.gameObject, Vector2.right)
            : Vector2.right;

        return TryResolveForwardGroundPosition(origin, direction, out landingPosition);
    }

    public List<Vector3> GetForwardGroundPositions(Vector3 origin, Transform directionSource)
    {
        Vector2 direction = directionSource != null
            ? AbilityAimResolver2D.Resolve(directionSource.gameObject, Vector2.right)
            : Vector2.right;

        return GetForwardGroundPositions(origin, direction);
    }

    public List<Vector3> GetForwardGroundPositions(Vector3 origin, Vector2 direction)
    {
        var positions = new List<Vector3>();
        var seenCells = new HashSet<string>();
        Vector2Int step = ToCardinalStep(direction);

        foreach (Tilemap map in GetGroundTilemaps())
        {
            if (!TryFindClosestGroundCell(map, origin, 1, out Vector3Int anchorCell))
                continue;

            foreach (Vector3Int offset in BuildForwardStripOffsets(step))
            {
                Vector3Int candidate = anchorCell + offset;
                if (!map.HasTile(candidate))
                    continue;

                string key = $"{map.GetInstanceID()}:{candidate.x}:{candidate.y}:{candidate.z}";
                if (!seenCells.Add(key))
                    continue;

                positions.Add(GetCellCenterWorld(map, candidate, origin.z));
            }
        }

        return positions;
    }

    public bool TryResolveForwardGroundPosition(Vector3 origin, Vector2 direction, out Vector3 landingPosition)
    {
        Vector2Int step = ToCardinalStep(direction);

        foreach (Tilemap map in GetGroundTilemaps())
        {
            if (!TryFindClosestGroundCell(map, origin, 1, out Vector3Int anchorCell))
                continue;

            foreach (Vector3Int offset in BuildDirectionalOffsets(step))
            {
                Vector3Int candidate = anchorCell + offset;
                if (!map.HasTile(candidate))
                    continue;

                landingPosition = GetCellCenterWorld(map, candidate, origin.z);
                return true;
            }
        }

        landingPosition = origin;
        return false;
    }

    private IEnumerable<Tilemap> GetGroundTilemaps()
    {
        Tilemap[] tilemaps = Object.FindObjectsOfType<Tilemap>(true);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap map = tilemaps[i];
            if (map != null && map.gameObject.layer == groundLayer)
                yield return map;
        }
    }

    private static bool TryFindClosestGroundCell(Tilemap map, Vector3 worldPosition, int searchRadius, out Vector3Int result)
    {
        Vector3Int originCell = map.WorldToCell(worldPosition);
        if (map.HasTile(originCell))
        {
            result = originCell;
            return true;
        }

        for (int radius = 1; radius <= searchRadius; radius++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    Vector3Int candidate = originCell + new Vector3Int(x, y, 0);
                    if (!map.HasTile(candidate))
                        continue;

                    result = candidate;
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    private static Vector3 GetCellCenterWorld(Tilemap map, Vector3Int cell, float z)
    {
        Vector3 center = map.GetCellCenterWorld(cell);
        center.z = z;
        return center;
    }

    private static Vector2Int ToCardinalStep(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector2Int.right;

        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return new Vector2Int(direction.x >= 0f ? 1 : -1, 0);

        return new Vector2Int(0, direction.y >= 0f ? 1 : -1);
    }

    private static IEnumerable<Vector3Int> BuildDirectionalOffsets(Vector2Int step)
    {
        Vector3Int primary = new Vector3Int(step.x, step.y, 0);
        Vector3Int secondary = new Vector3Int(step.x * 2, step.y * 2, 0);

        if (step.x != 0)
        {
            yield return primary;
            yield return new Vector3Int(step.x, 1, 0);
            yield return new Vector3Int(step.x, -1, 0);
            yield return secondary;
            yield return new Vector3Int(step.x * 2, 1, 0);
            yield return new Vector3Int(step.x * 2, -1, 0);
        }
        else
        {
            yield return primary;
            yield return new Vector3Int(1, step.y, 0);
            yield return new Vector3Int(-1, step.y, 0);
            yield return secondary;
            yield return new Vector3Int(1, step.y * 2, 0);
            yield return new Vector3Int(-1, step.y * 2, 0);
        }

        yield return Vector3Int.zero;
    }

    private static IEnumerable<Vector3Int> BuildForwardStripOffsets(Vector2Int step)
    {
        yield return new Vector3Int(step.x, step.y, 0);

        if (step.x != 0)
        {
            yield return new Vector3Int(step.x, 1, 0);
            yield return new Vector3Int(step.x, -1, 0);
        }
        else
        {
            yield return new Vector3Int(1, step.y, 0);
            yield return new Vector3Int(-1, step.y, 0);
        }
    }

    private static IEnumerable<int> BuildHorizontalOffsets(int radius)
    {
        yield return 0;

        for (int distance = 1; distance <= radius; distance++)
        {
            yield return -distance;
            yield return distance;
        }
    }
}
