using System.Collections.Generic;
using UnityEngine;

/// <summary>Supplies live positions without handing ownership to a placement tool.</summary>
internal interface IPlacementPositions
{
    int Count { get; }
    bool TryGetPosition(int index, out Vector2 position);
}

/// <summary>Position-only consumers can supply a maintained coordinate list.</summary>
internal readonly struct PositionListReader : IPlacementPositions
{
    private readonly List<Vector2> positions;
    internal PositionListReader(List<Vector2> positions) => this.positions = positions;
    public int Count => positions.Count;
    public bool TryGetPosition(int index, out Vector2 position)
    {
        position = positions[index];
        return true;
    }
}

/// <summary>A position plan is a value; the content owner performs actual spawning.</summary>
internal readonly struct PlacementPoint
{
    internal readonly Vector2 position;
    internal readonly MonsterRoomArea2D room;

    internal PlacementPoint(Vector2 position, MonsterRoomArea2D room)
    {
        this.position = position;
        this.room = room;
    }
}

/// <summary>Fixed numeric and spatial conditions for one synchronous request.</summary>
internal readonly struct PlacementSettings
{
    internal readonly Vector2 origin;
    internal readonly float range;
    internal readonly MonsterRoomArea2D region;
    internal readonly float minOriginDistance;
    internal readonly float minSpacing;
    internal readonly float pathRadius;
    internal readonly int pathBlockedMask;
    internal readonly float landingRadius;
    internal readonly int landingBlockedMask;
    internal readonly int requiredGroundMask;
    internal readonly bool requireGround;
    internal readonly bool rejectWallTiles;
    internal readonly bool allowSeparatingInitialOverlap;
    internal readonly bool reuseGeometry;

    internal PlacementSettings(Vector2 origin, float range, MonsterRoomArea2D region,
        float minOriginDistance, float minSpacing, float pathRadius, int pathBlockedMask,
        float landingRadius, int landingBlockedMask, int requiredGroundMask,
        bool requireGround, bool rejectWallTiles, bool allowSeparatingInitialOverlap, bool reuseGeometry)
    {
        this.origin = origin;
        this.range = range;
        this.region = region;
        this.minOriginDistance = minOriginDistance;
        this.minSpacing = minSpacing;
        this.pathRadius = pathRadius;
        this.pathBlockedMask = pathBlockedMask;
        this.landingRadius = landingRadius;
        this.landingBlockedMask = landingBlockedMask;
        this.requiredGroundMask = requiredGroundMask;
        this.requireGround = requireGround;
        this.rejectWallTiles = rejectWallTiles;
        this.allowSeparatingInitialOverlap = allowSeparatingInitialOverlap;
        this.reuseGeometry = reuseGeometry;
    }
}

/// <summary>
/// Owns validation, growing reservations and geometry memoization for one synchronous
/// request. The value-type reader preserves live reads without per-point boxing.
/// </summary>
internal sealed class PlacementSession<TPositions> where TPositions : struct, IPlacementPositions
{
    private readonly MapQuery2D maps;
    private readonly PhysicsQuery2D physics;
    private readonly TPositions positions;
    private readonly List<PlacementPoint> pending = new List<PlacementPoint>();
    private readonly Dictionary<Vector2, bool> geometryResults = new Dictionary<Vector2, bool>();
    private PlacementSettings settings;

    internal bool IsActive { get; private set; }
    internal int GeometryCacheHits { get; private set; }
    internal int Count => pending.Count;
    internal List<PlacementPoint> Pending => pending;

#if UNITY_EDITOR
    internal PlacementCounters Counters;
#endif

    internal PlacementSession(MapQuery2D maps, PhysicsQuery2D physics, TPositions positions)
    {
        this.maps = maps;
        this.physics = physics;
        this.positions = positions;
    }

    internal void Begin(PlacementSettings requestSettings, bool clearReservations = true)
    {
        settings = requestSettings;
        if (clearReservations) pending.Clear();
        geometryResults.Clear();
        GeometryCacheHits = 0;
        IsActive = true;
    }

    internal void End()
    {
        IsActive = false;
        geometryResults.Clear();
    }

    internal bool CanPlace(Vector2 candidate)
    {
#if UNITY_EDITOR
        if (Counters != null) Counters.candidateChecks++;
#endif
        if (!float.IsFinite(candidate.x) || !float.IsFinite(candidate.y)) return false;
        if (Vector2.Distance(settings.origin, candidate) > settings.range) return false;
        if (settings.region != null && !settings.region.Contains(candidate)) return false;
        if (Vector2.Distance(settings.origin, candidate) < settings.minOriginDistance) return false;
        if (!HasRequiredSpacing(candidate)) return false;
        if (IsActive && settings.reuseGeometry && geometryResults.TryGetValue(candidate, out bool remembered))
        {
            GeometryCacheHits++;
            return remembered;
        }
        bool valid = (!settings.rejectWallTiles || !maps.HasWallTileAt(candidate)) &&
            !physics.HasBlocker(settings.origin, candidate, settings.pathRadius,
                settings.pathBlockedMask, settings.allowSeparatingInitialOverlap) &&
            !physics.HasOverlap(candidate, settings.landingRadius, settings.landingBlockedMask) &&
            (!settings.requireGround || maps.HasGroundTileAt(candidate) ||
                physics.HasOverlap(candidate, settings.landingRadius, settings.requiredGroundMask));
        if (IsActive && settings.reuseGeometry) geometryResults[candidate] = valid;
        return valid;
    }

    internal PlacementPoint Commit(Vector2 position)
    {
        var point = new PlacementPoint(position,
            settings.region != null ? settings.region : maps.FindRoomContaining(position));
        pending.Add(point);
        return point;
    }

    private bool HasRequiredSpacing(Vector2 candidate)
    {
        if (settings.minSpacing <= 0f) return true;
        float spacingSquared = settings.minSpacing * settings.minSpacing;
        for (int i = positions.Count - 1; i >= 0; i--)
            if (positions.TryGetPosition(i, out Vector2 existing) &&
                (existing - candidate).sqrMagnitude < spacingSquared) return false;
        for (int i = 0; i < pending.Count; i++)
            if ((pending[i].position - candidate).sqrMagnitude < spacingSquared) return false;
        return true;
    }
}
