using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Binds the existing weapon settings and authored map convention to common query tools.</summary>
internal sealed class LightningSpearPlacementWorldQuery
{
    private MapQuery2D maps;
    internal MapQuery2D Maps => maps ??= CreateMapQuery();
    internal PhysicsQuery2D Physics { get; } = new PhysicsQuery2D();
#if UNITY_EDITOR
    internal PlacementCounters Counters { set => Physics.Counters = value; }
#endif
    private static MapQuery2D CreateMapQuery()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        return new MapQuery2D(groundLayer >= 0 ? groundLayer : 7, LayerMask.NameToLayer("Wall"), "Wall");
    }
    internal List<Collider2D> FindTargetHits(Vector2 center, float radius) => Physics.OverlapTargets(center, radius);
    internal void ClearPlacementResults() => Physics.ClearPlacementResults();
    internal void InvalidateTilemapCache() => Maps.InvalidateTilemapCache();
    internal List<Tilemap> ResolveGroundTilemaps() => Maps.ResolveGroundTilemaps();
    internal bool HasWallTileAt(Vector2 position) => Maps.HasWallTileAt(position);
    internal bool HasGroundTileAt(Vector2 position) => Maps.HasGroundTileAt(position);
    internal MonsterRoomArea2D FindRoomContaining(Vector2 position) => Maps.FindRoomContaining(position);
    internal static MonsterRoomArea2D[] FindRooms() => MapQuery2D.FindRooms();
    internal bool HasRequiredGroundOverlap(LightningSpearLoadout loadout, Vector2 position, float radius) =>
        Physics.HasOverlap(position, radius, loadout != null ? loadout.RequiredGroundLayers.value : 0);
    internal bool IsLandingValid(LightningSpearLoadout loadout, LightningSpearSkill2Data data, Vector2 position)
    {
        if (loadout == null) return false;
        float radius = data != null ? data.LandingProbeRadius : loadout.LandingProbeRadius;
        return !Physics.HasOverlap(position, radius, loadout.LandingBlockedMask) &&
            (Maps.HasGroundTileAt(position) || HasRequiredGroundOverlap(loadout, position, radius));
    }
    internal bool HasPlacementBlocker(Vector2 start, Vector2 end, float radius, int mask) =>
        Physics.HasBlocker(start, end, radius, mask, true);
    internal static bool HasBlocker(Vector2 start, Vector2 end, float radius, int mask) =>
        PhysicsQuery2D.HasBlockerAll(start, end, radius, mask, true);
    internal static bool IsInitialPlacementOverlapMovingAway(Vector2 start, Vector2 direction,
        Collider2D collider, RaycastHit2D hit) =>
        PhysicsQuery2D.IsInitialOverlapMovingAway(start, direction, collider, hit);
}
