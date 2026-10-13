#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Owns scratch data for one synchronous placement request. Geometry stays fixed
/// while reservations only grow; rejected nearest candidates never become eligible.
/// </summary>
internal sealed class LightningSpearPlacementSearch
{
    internal sealed class NearestCandidates
    {
        private readonly struct Candidate
        {
            internal readonly Vector2 position;
            internal readonly float distanceSquared;

            internal Candidate(Vector2 position, Vector2 preferred)
            {
                this.position = position;
                distanceSquared = (position - preferred).sqrMagnitude;
            }
        }

        private readonly List<Candidate> heap = new List<Candidate>();

        internal void Reset(List<Vector2> candidates, Vector2 target)
        {
            heap.Clear();
            foreach (Vector2 candidate in candidates)
                heap.Add(new Candidate(candidate, target));
            for (int i = heap.Count / 2 - 1; i >= 0; i--)
                SiftDown(i);
        }

        internal bool TryPeek(out Vector2 candidate)
        {
            candidate = heap.Count > 0 ? heap[0].position : default;
            return heap.Count > 0;
        }

        internal void RejectCurrent()
        {
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            if (heap.Count > 0)
                SiftDown(0);
        }

        private void SiftDown(int index)
        {
            Candidate value = heap[index];
            while (index < heap.Count / 2)
            {
                int child = index * 2 + 1;
                if (child + 1 < heap.Count && Compare(heap[child + 1], heap[child]) < 0)
                    child++;
                if (Compare(value, heap[child]) <= 0)
                    break;
                heap[index] = heap[child];
                index = child;
            }
            heap[index] = value;
        }

        private static int Compare(Candidate a, Candidate b)
        {
            int distance = a.distanceSquared.CompareTo(b.distanceSquared);
            if (distance != 0)
                return distance;
            int x = a.position.x.CompareTo(b.position.x);
            return x != 0 ? x : a.position.y.CompareTo(b.position.y);
        }
    }

    private readonly List<Vector2> candidates = new List<Vector2>();
    private readonly HashSet<Vector2> uniqueCandidates = new HashSet<Vector2>();
    private readonly Dictionary<Vector2, bool> geometryResults = new Dictionary<Vector2, bool>();
    private readonly Dictionary<Vector2, NearestCandidates> searches = new Dictionary<Vector2, NearestCandidates>();
    private readonly HashSet<Vector2> exhaustedTargets = new HashSet<Vector2>();
    private readonly List<NearestCandidates> searchPool = new List<NearestCandidates>();
    private readonly Stack<NearestCandidates> availableSearches = new Stack<NearestCandidates>();
    private readonly NearestCandidates exhaustedSearch = new NearestCandidates();
    private Vector2 origin;
    private float range;
    private bool candidatesCollected;
    private bool cacheGeometry;
    private int usedSearches;

    internal bool IsActive { get; private set; }
    internal int CollectionCount { get; private set; }
    internal int VisitedCells { get; private set; }
    internal int UniqueCandidateCount => candidates.Count;
    internal int HeapBuildCount => usedSearches;
    internal int GeometryCacheHits { get; private set; }

    internal void Begin(Vector2 ownerPosition, float castRange, bool reuseGeometry = true)
    {
        origin = ownerPosition;
        range = castRange;
        cacheGeometry = reuseGeometry;
        candidates.Clear();
        uniqueCandidates.Clear();
        geometryResults.Clear();
        searches.Clear();
        exhaustedTargets.Clear();
        availableSearches.Clear();
        foreach (NearestCandidates search in searchPool)
            availableSearches.Push(search);
        candidatesCollected = false;
        usedSearches = 0;
        CollectionCount = 0;
        VisitedCells = 0;
        GeometryCacheHits = 0;
        IsActive = true;
    }

    internal void End()
    {
        // Clear request identity/results without discarding reusable collection capacity.
        IsActive = false;
        searches.Clear();
        exhaustedTargets.Clear();
        geometryResults.Clear();
    }

    internal bool TryGetGeometry(Vector2 position, out bool valid)
    {
        if (IsActive && cacheGeometry && geometryResults.TryGetValue(position, out valid))
        {
            GeometryCacheHits++;
            return true;
        }
        valid = false;
        return false;
    }

    internal void RememberGeometry(Vector2 position, bool valid)
    {
        if (IsActive && cacheGeometry)
            geometryResults[position] = valid;
    }

    internal NearestCandidates GetNearest(Vector2 preferred, List<Tilemap> groundTilemaps)
    {
        if (exhaustedTargets.Contains(preferred))
            return exhaustedSearch;
        if (searches.TryGetValue(preferred, out NearestCandidates existing))
            return existing;

        if (!candidatesCollected)
            CollectCandidates(groundTilemaps);
        NearestCandidates search;
        if (availableSearches.Count > 0)
            search = availableSearches.Pop();
        else
        {
            search = new NearestCandidates();
            searchPool.Add(search);
        }
        usedSearches++;
        search.Reset(candidates, preferred);
        searches.Add(preferred, search);
        return search;
    }

    internal void RememberExhausted(Vector2 preferred)
    {
        if (!searches.TryGetValue(preferred, out NearestCandidates search))
            return;
        searches.Remove(preferred);
        exhaustedTargets.Add(preferred);
        // Only successful targets retain progress. Failed targets share a reusable
        // scratch heap instead of retaining one O(T) buffer per enemy.
        availableSearches.Push(search);
    }

    private void CollectCandidates(List<Tilemap> groundTilemaps)
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
}
#endif
