using System.Collections.Generic;
using UnityEngine;

/// <summary>Bounded collection and fallback operations over a caller-owned placement session.</summary>
internal static class PlacementFlow
{
    internal static void FillWithinBudget<TPositions>(ref PositionCandidateSequence source,
        int attempts, int desiredTotal, PlacementSession<TPositions> session)
        where TPositions : struct, IPlacementPositions
    {
        for (int i = 0; i < attempts && session.Count < desiredTotal; i++)
        {
            if (!source.TryNext(out Vector2 candidate))
                break;
            if (session.CanPlace(candidate))
                session.Commit(candidate);
        }
    }

    internal static void RoundRobinFill<TPositions>(List<Transform> targets, int desiredTotal,
        NearestPositionSearch search, MapQuery2D maps, PlacementSession<TPositions> session)
        where TPositions : struct, IPlacementPositions
    {
        while (session.Count < desiredTotal)
        {
            int previousCount = session.Count;
            foreach (Transform target in targets)
            {
                if (target == null || !search.TryFind(target.position, maps, session, out Vector2 candidate))
                    continue;
                session.Commit(candidate);
                if (session.Count >= desiredTotal)
                    return;
            }
            if (session.Count == previousCount)
                break;
        }
    }

    internal static bool FirstSuccess<TPositions>(Vector2 preferred, NearestPositionSearch search,
        MapQuery2D maps, ref PositionCandidateSequence fallback, int fallbackBudget,
        PlacementSession<TPositions> session, out PlacementPoint point)
        where TPositions : struct, IPlacementPositions
    {
        if (search.TryFind(preferred, maps, session, out Vector2 nearest))
        {
            point = session.Commit(nearest);
            return true;
        }
        // The fixed prefix counts against this budget and may revisit the rejected
        // preferred point. No random sample is drawn until both fixed points are consumed.
        for (int i = 0; i < fallbackBudget; i++)
        {
            if (!fallback.TryNext(out Vector2 candidate))
                break;
            if (!session.CanPlace(candidate))
                continue;
            point = session.Commit(candidate);
            return true;
        }
        point = default;
        return false;
    }
}
