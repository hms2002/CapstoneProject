using UnityEngine;
using UnityEngine.Tilemaps;

public static class PitFallPositionResolver
{
    private static readonly Vector3 DefaultFootOffset = new Vector3(0f, -0.5f, 0f);

    private static readonly Vector3Int[] NeighborOffsets =
    {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right,
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 1, 0),
        new Vector3Int(-1, -1, 0)
    };

    public static Vector3 ResolveFallCenter(Vector3 targetPosition, GameObject trapObject)
    {
        return ResolveFallCenter(targetPosition, trapObject, DefaultFootOffset);
    }

    public static Vector3 ResolveFallCenter(Vector3 targetPosition, GameObject trapObject, Vector3 footOffset)
    {
        if (trapObject == null)
            return targetPosition;

        Tilemap tilemap = ResolveTilemap(trapObject);
        if (tilemap == null)
            return trapObject.transform.position;

        Vector3 footPosition = targetPosition + footOffset;
        Vector3Int cellPosition = tilemap.WorldToCell(footPosition);

        if (tilemap.HasTile(cellPosition))
            return tilemap.GetCellCenterWorld(cellPosition);

        return ResolveNearestNeighborTileCenter(tilemap, cellPosition, footPosition, targetPosition);
    }

    private static Tilemap ResolveTilemap(GameObject trapObject)
    {
        Tilemap tilemap = trapObject.GetComponent<Tilemap>();
        return tilemap != null ? tilemap : trapObject.GetComponentInChildren<Tilemap>();
    }

    private static Vector3 ResolveNearestNeighborTileCenter(
        Tilemap tilemap,
        Vector3Int cellPosition,
        Vector3 footPosition,
        Vector3 fallbackPosition)
    {
        float minDistance = float.MaxValue;
        Vector3 bestPosition = fallbackPosition;

        for (int i = 0; i < NeighborOffsets.Length; i++)
        {
            Vector3Int neighbor = cellPosition + NeighborOffsets[i];
            if (!tilemap.HasTile(neighbor))
                continue;

            Vector3 centerWorld = tilemap.GetCellCenterWorld(neighbor);
            float distance = Vector3.Distance(footPosition, centerWorld);
            if (distance >= minDistance)
                continue;

            minDistance = distance;
            bestPosition = centerWorld;
        }

        return bestPosition;
    }
}
