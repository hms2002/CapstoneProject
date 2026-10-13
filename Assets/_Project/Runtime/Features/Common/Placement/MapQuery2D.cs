using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Queries tile and room facts using caller-supplied map classification and a frame-local cache.</summary>
internal sealed class MapQuery2D
{
    private readonly int groundLayer;
    private readonly int wallLayer;
    private readonly string wallTilemapName;
    private readonly List<Tilemap> groundTilemapCache = new List<Tilemap>();
    private readonly List<Tilemap> wallTilemapCache = new List<Tilemap>();
    private int groundTilemapCacheFrame = -1;

    internal MapQuery2D(int groundLayer, int wallLayer, string wallTilemapName)
    {
        this.groundLayer = groundLayer;
        this.wallLayer = wallLayer;
        this.wallTilemapName = wallTilemapName;
    }

    internal List<Tilemap> ResolveGroundTilemaps()
    {
        if (groundTilemapCacheFrame == Time.frameCount)
            return groundTilemapCache;

        groundTilemapCacheFrame = Time.frameCount;
        groundTilemapCache.Clear();
        wallTilemapCache.Clear();
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
        Tilemap[] tilemaps = Object.FindObjectsOfType<Tilemap>();
#endif
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (!IsUsableGroundTilemap(tilemap))
                continue;

            if (tilemap.gameObject.layer == wallLayer || tilemap.name == wallTilemapName)
                wallTilemapCache.Add(tilemap);
            else if (tilemap.gameObject.layer == groundLayer)
                groundTilemapCache.Add(tilemap);
        }

        return groundTilemapCache;
    }

    internal bool HasGroundTileAt(Vector2 position)
    {
        List<Tilemap> groundTilemaps = ResolveGroundTilemaps();
        if (groundTilemaps.Count == 0)
            return false;

        for (int i = 0; i < groundTilemaps.Count; i++)
        {
            Tilemap tilemap = groundTilemaps[i];
            if (!IsUsableGroundTilemap(tilemap))
                continue;

            if (tilemap.HasTile(tilemap.WorldToCell(position)))
                return true;
        }

        return false;
    }

    internal bool HasWallTileAt(Vector2 position)
    {
        ResolveGroundTilemaps();
        for (int i = 0; i < wallTilemapCache.Count; i++)
        {
            Tilemap tilemap = wallTilemapCache[i];
            if (tilemap != null && tilemap.isActiveAndEnabled && tilemap.gameObject.activeInHierarchy &&
                tilemap.HasTile(tilemap.WorldToCell(position)))
                return true;
        }

        return false;
    }

    internal void InvalidateTilemapCache()
    {
        groundTilemapCacheFrame = -1;
    }

    internal MonsterRoomArea2D FindRoomContaining(Vector2 position)
    {
        MonsterRoomArea2D[] rooms = FindRooms();
        for (int i = 0; i < rooms.Length; i++)
        {
            MonsterRoomArea2D room = rooms[i];
            if (room != null && room.Contains(position))
                return room;
        }

        return null;
    }

    internal static MonsterRoomArea2D[] FindRooms()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<MonsterRoomArea2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<MonsterRoomArea2D>();
#endif
    }

    private static bool IsUsableGroundTilemap(Tilemap tilemap)
    {
        return tilemap != null && tilemap.isActiveAndEnabled && tilemap.gameObject.activeInHierarchy;
    }
}
