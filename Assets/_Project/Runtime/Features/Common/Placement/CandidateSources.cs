using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

internal delegate bool PlacementTargetResolver(Collider2D hit, out Transform target);

/// <summary>Reusable candidate and target buffers for one synchronous placement request.</summary>
internal sealed class CandidateSources
{
    private readonly List<Vector2> candidates = new List<Vector2>();
    private readonly HashSet<Vector2> uniqueCandidates = new HashSet<Vector2>();
    private readonly List<Transform> targets = new List<Transform>();
    private readonly HashSet<Transform> seenTargets = new HashSet<Transform>();
    private readonly System.Comparison<Transform> compareTargets;
    private Vector2 origin;
    private float range;
    private Vector2 targetSortCenter;
    private bool candidatesCollected;

    internal int CollectionCount { get; private set; }
    internal int VisitedCells { get; private set; }
    internal int UniqueCandidateCount => candidates.Count;

#if UNITY_EDITOR
    internal PlacementCounters Counters;
#endif

    internal CandidateSources() => compareTargets = CompareTargetDistance;

    internal void Begin(Vector2 ownerPosition, float castRange)
    {
        origin = ownerPosition;
        range = castRange;
        candidates.Clear();
        uniqueCandidates.Clear();
        targets.Clear();
        seenTargets.Clear();
        candidatesCollected = false;
        CollectionCount = 0;
        VisitedCells = 0;
    }

    internal void End()
    {
        targets.Clear();
        seenTargets.Clear();
    }

    internal List<Vector2> GetTileCandidates(List<Tilemap> groundTilemaps)
    {
        if (!candidatesCollected)
            CollectTileCandidates(groundTilemaps);
        return candidates;
    }

    internal List<Transform> FindTargets(PhysicsQuery2D physics, Vector2 center, float radius,
        PlacementTargetResolver resolver)
    {
        targets.Clear();
        seenTargets.Clear();
        foreach (Collider2D hit in physics.OverlapTargets(center, radius))
            if (resolver(hit, out Transform target) && seenTargets.Add(target))
                targets.Add(target);
        targetSortCenter = center;
        // Retain the distance-only comparison and the same Comparison overload.
        // Equal-distance targets keep the existing sort behavior; no new tie rule is added.
        targets.Sort(compareTargets);
#if UNITY_EDITOR
        if (Counters != null) Counters.targetsFound = targets.Count;
#endif
        return targets;
    }

    private int CompareTargetDistance(Transform a, Transform b) =>
        ((Vector2)a.position - targetSortCenter).sqrMagnitude.CompareTo(
            ((Vector2)b.position - targetSortCenter).sqrMagnitude);

    private void CollectTileCandidates(List<Tilemap> groundTilemaps)
    {
        candidatesCollected = true;
        CollectionCount++;
        float rangeSquared = range * range;
        foreach (Tilemap tilemap in groundTilemaps)
        {
            Vector3Int lower = tilemap.WorldToCell(origin - Vector2.one * range);
            Vector3Int upper = lower;
            for (int corner = 1; corner < 4; corner++)
            {
                Vector2 offset = new Vector2((corner & 1) == 0 ? -range : range,
                    (corner & 2) == 0 ? -range : range);
                Vector3Int cell = tilemap.WorldToCell(origin + offset);
                lower = Vector3Int.Min(lower, cell);
                upper = Vector3Int.Max(upper, cell);
            }
            BoundsInt bounds = tilemap.cellBounds;
            lower = Vector3Int.Max(bounds.min, lower);
            upper = Vector3Int.Min(bounds.max, upper + Vector3Int.one);
            if (lower.x >= upper.x || lower.y >= upper.y || lower.z >= upper.z)
                continue;
            bounds.SetMinMax(lower, upper);
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                VisitedCells++;
                Vector2 point = tilemap.GetCellCenterWorld(cell);
                if ((point - origin).sqrMagnitude <= rangeSquared && tilemap.HasTile(cell) &&
                    uniqueCandidates.Add(point))
                    candidates.Add(point);
            }
        }
    }

    internal static PositionCandidateSequence RandomDisk(Vector2 origin, float radius) =>
        new PositionCandidateSequence(default, default, origin, radius, false);

    internal static PositionCandidateSequence FixedThenRandom(Vector2 first, Vector2 second,
        Vector2 origin, float radius) => new PositionCandidateSequence(first, second, origin, radius, true);
}

/// <summary>A value cursor. Random samples are drawn only when the flow requests the next point.</summary>
internal struct PositionCandidateSequence
{
    private readonly Vector2 first;
    private readonly Vector2 second;
    private readonly Vector2 origin;
    private readonly float radius;
    private readonly bool hasFixedPrefix;
    private int fixedIndex;

    internal PositionCandidateSequence(Vector2 first, Vector2 second, Vector2 origin, float radius,
        bool hasFixedPrefix)
    {
        this.first = first;
        this.second = second;
        this.origin = origin;
        this.radius = radius;
        this.hasFixedPrefix = hasFixedPrefix;
        fixedIndex = 0;
    }

    internal bool TryNext(out Vector2 position)
    {
        if (hasFixedPrefix && fixedIndex < 2)
        {
            position = fixedIndex == 0 ? first : second;
            fixedIndex++;
        }
        else
        {
            position = origin + Random.insideUnitCircle * Mathf.Max(0.01f, radius);
        }
        return true;
    }
}

#if UNITY_EDITOR
[System.Serializable]
internal sealed class PlacementCounters
{
    public int candidateChecks;
    public int candidateCollections;
    public int visitedCells;
    public int fullSorts;
    public int heapBuilds;
    public int geometryCacheHits;
    public int targetOverlapQueries;
    public int targetsFound;
    public int pathQueries;
    public int landingOverlapQueries;
}
#endif
