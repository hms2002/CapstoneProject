using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Reuses nearest-point cursors during one synchronous request. Rejected points cannot
/// become eligible again while geometry is fixed and reservations only grow.
/// </summary>
internal sealed class NearestPositionSearch
{
    private sealed class NearestCandidates
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

    private readonly CandidateSources sources;
    private readonly Dictionary<Vector2, NearestCandidates> searches = new Dictionary<Vector2, NearestCandidates>();
    private readonly HashSet<Vector2> exhaustedTargets = new HashSet<Vector2>();
    private readonly List<NearestCandidates> searchPool = new List<NearestCandidates>();
    private readonly Stack<NearestCandidates> availableSearches = new Stack<NearestCandidates>();
    private readonly NearestCandidates exhaustedSearch = new NearestCandidates();
    private int usedSearches;

    internal bool IsActive { get; private set; }
    internal int CollectionCount => sources.CollectionCount;
    internal int VisitedCells => sources.VisitedCells;
    internal int UniqueCandidateCount => sources.UniqueCandidateCount;
    internal int HeapBuildCount => usedSearches;

    internal NearestPositionSearch(CandidateSources sources) => this.sources = sources;

    internal void Begin(Vector2 origin, float range)
    {
        sources.Begin(origin, range);
        searches.Clear();
        exhaustedTargets.Clear();
        availableSearches.Clear();
        foreach (NearestCandidates search in searchPool)
            availableSearches.Push(search);
        usedSearches = 0;
        IsActive = true;
    }

    internal void End()
    {
        IsActive = false;
        searches.Clear();
        exhaustedTargets.Clear();
        sources.End();
    }

    internal bool TryFind<TPositions>(Vector2 preferred, MapQuery2D maps,
        PlacementSession<TPositions> session, out Vector2 result) where TPositions : struct, IPlacementPositions
    {
        // A valid exact position is never snapped to a tile center.
        if (session.CanPlace(preferred))
        {
            result = preferred;
            return true;
        }
        NearestCandidates nearest = GetNearest(preferred, maps.ResolveGroundTilemaps());
        while (nearest.TryPeek(out Vector2 candidate))
        {
            if (session.CanPlace(candidate))
            {
                // Success retains the point. A later reservation may reject it; with
                // zero spacing, returning the same successful point remains allowed.
                result = candidate;
                return true;
            }
            nearest.RejectCurrent();
        }
        RememberExhausted(preferred);
        result = default;
        return false;
    }

    private NearestCandidates GetNearest(Vector2 preferred, List<Tilemap> groundTilemaps)
    {
        if (exhaustedTargets.Contains(preferred))
            return exhaustedSearch;
        if (searches.TryGetValue(preferred, out NearestCandidates existing))
            return existing;
        List<Vector2> candidates = sources.GetTileCandidates(groundTilemaps);
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

    private void RememberExhausted(Vector2 preferred)
    {
        if (!searches.TryGetValue(preferred, out NearestCandidates search))
            return;
        searches.Remove(preferred);
        exhaustedTargets.Add(preferred);
        // Recycle failed cursors instead of retaining a full heap for every target.
        availableSearches.Push(search);
    }
}
