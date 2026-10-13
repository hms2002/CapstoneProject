using System.Collections.Generic;
using UnityEngine;

/// <summary>Queries overlap and swept-circle facts with reusable results and explicit initial-overlap policy.</summary>
internal sealed class PhysicsQuery2D
{
    private readonly List<Collider2D> targetHits = new List<Collider2D>();
    private readonly List<RaycastHit2D> placementCastHits = new List<RaycastHit2D>();

#if UNITY_EDITOR
    internal PlacementCounters Counters;
#endif

    internal List<Collider2D> OverlapTargets(Vector2 center, float radius)
    {
        ContactFilter2D filter = new ContactFilter2D().NoFilter();
        filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        filter.useTriggers = Physics2D.queriesHitTriggers;
#if UNITY_EDITOR
        if (Counters != null) Counters.targetOverlapQueries++;
#endif
        Physics2D.OverlapCircle(center, radius, filter, targetHits);
        return targetHits;
    }

    internal void ClearPlacementResults()
    {
        targetHits.Clear();
        placementCastHits.Clear();
    }

    internal bool HasOverlap(Vector2 position, float radius, int mask)
    {
        if (mask == 0)
            return false;
#if UNITY_EDITOR
        if (Counters != null) Counters.landingOverlapQueries++;
#endif
        return Physics2D.OverlapCircle(position, radius, mask) != null;
    }

    internal bool HasBlocker(Vector2 start, Vector2 end, float radius, int mask,
        bool allowSeparatingInitialOverlap)
    {
        if (mask == 0)
            return false;

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        Vector2 direction = delta / distance;
#if UNITY_EDITOR
        if (Counters != null) Counters.pathQueries++;
#endif
        if (radius <= 0f)
            return Physics2D.Linecast(start, end, mask).collider != null;

        ContactFilter2D filter = new ContactFilter2D().NoFilter();
        filter.SetLayerMask(mask);
        filter.useTriggers = Physics2D.queriesHitTriggers;
        Physics2D.CircleCast(start, radius, direction, filter, placementCastHits, distance);
        for (int i = 0; i < placementCastHits.Count; i++)
        {
            RaycastHit2D hit = placementCastHits[i];
            Collider2D collider = hit.collider;
            if (collider == null)
                continue;

            if (allowSeparatingInitialOverlap && IsInitialOverlapMovingAway(start, direction, collider, hit))
                continue;

            return true;
        }

        return false;
    }

    internal static bool HasBlockerAll(Vector2 start, Vector2 end, float radius, int mask,
        bool allowSeparatingInitialOverlap)
    {
        if (mask == 0)
            return false;

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        Vector2 direction = delta / distance;
        if (radius > 0f)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(start, radius, direction, distance, mask);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null)
                    continue;

                if (allowSeparatingInitialOverlap && IsInitialOverlapMovingAway(start, direction, collider, hits[i]))
                    continue;

                return true;
            }

            return false;
        }

        return Physics2D.Linecast(start, end, mask).collider != null;
    }

    internal static bool IsInitialOverlapMovingAway(
        Vector2 start,
        Vector2 direction,
        Collider2D collider,
        RaycastHit2D hit)
    {
        if (hit.distance > 0.001f && hit.fraction > 0.0001f)
            return false;

        Vector2 closest = collider.ClosestPoint(start);
        Vector2 awayFromCollider = start - closest;
        if (awayFromCollider.sqrMagnitude <= 0.000001f)
            return false;

        return Vector2.Dot(direction, awayFromCollider.normalized) >= -0.05f;
    }
}
