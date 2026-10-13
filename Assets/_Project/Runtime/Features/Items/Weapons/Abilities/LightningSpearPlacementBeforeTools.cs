#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Independent snapshot of the last weapon-only split. Measurement only, excluded from players.
internal sealed class LightningSpearPlacementBeforeTools
{
    private readonly LightningSpearWorldBeforeTools world;
    // Borrow the owner's list and only read it; keep the existing direct list access
    // in the per-candidate spacing loop instead of interface dispatch.
    private readonly List<LightningSpearMarkActor> activeMarks;
    private readonly List<PlacementPoint> pendingMarkSpawns = new List<PlacementPoint>();
    private readonly LightningSpearPlacementSearch search = new LightningSpearPlacementSearch();
    private readonly List<Transform> markTargets = new List<Transform>();
    private readonly HashSet<Transform> seenMarkTargets = new HashSet<Transform>();

    internal LightningSpearPlacementSearch Search => search;
    internal List<PlacementPoint> Pending => pendingMarkSpawns;

#if UNITY_EDITOR
    internal PlacementCounters Counters;
#endif

    internal LightningSpearPlacementBeforeTools(List<LightningSpearMarkActor> activeMarks)
    {
        this.world = new LightningSpearWorldBeforeTools();
        this.activeMarks = activeMarks;
    }

    /// <summary>The returned list is borrowed until the next request; copy it before yielding.</summary>
    internal List<PlacementPoint> GenerateMarkPositions(LightningSpearLoadout loadout,
        LightningSpearSkill2Data data, Vector2 ownerPosition, bool targeted)
    {
        RemoveDestroyedMarks();
        BeginRequest(loadout, data, ownerPosition, targeted);
        try
        {
            int targetCount = GetMarkRainCount(loadout, data);
            if (targetCount <= 0)
                return pendingMarkSpawns;

            MonsterRoomArea2D ownerRoom = world.FindRoomContaining(ownerPosition);
            if (targeted)
            {
                List<Transform> targets = FindMarkTargets(ownerPosition, GetFallbackCombatRadius(loadout, data));
                // Give each enemy a direct strike first, then reserve the nearest free tiles around them.
                while (pendingMarkSpawns.Count < targetCount)
                {
                    int previousCount = pendingMarkSpawns.Count;
                    foreach (Transform target in targets)
                    {
                        if (target == null || !TryFindNearest(loadout, data, ownerPosition,
                            ownerRoom, target.position, out Vector2 candidate)) continue;
                        pendingMarkSpawns.Add(new PlacementPoint(candidate,
                            ownerRoom != null ? ownerRoom : world.FindRoomContaining(candidate)));
                        if (pendingMarkSpawns.Count >= targetCount) return pendingMarkSpawns;
                    }
                    if (pendingMarkSpawns.Count == previousCount) break;
                }
            }
            int sampleCount = Mathf.Max(GetCandidateSamples(loadout, data), targetCount * 12);
            for (int i = 0; i < sampleCount && pendingMarkSpawns.Count < targetCount; i++)
            {
                Vector2 candidate = SampleFallbackCandidate(ownerPosition, GetFallbackCombatRadius(loadout, data));
                if (!ValidateCandidate(loadout, data, ownerPosition, ownerRoom, candidate))
                    continue;

                MonsterRoomArea2D markRoom = ownerRoom != null ? ownerRoom : world.FindRoomContaining(candidate);
                pendingMarkSpawns.Add(new PlacementPoint(candidate, markRoom));
            }
            return pendingMarkSpawns;
        }
        finally
        {
            EndRequest();
        }
    }

    internal bool TryPlanThirdStrike(LightningSpearLoadout loadout, LightningSpearSkill2Data data,
        Vector2 origin, Vector2 attackTip, out PlacementPoint request)
    {
        BeginRequest(loadout, data, origin);
        try
        {
            MonsterRoomArea2D room = world.FindRoomContaining(origin);
            List<Transform> targets = FindMarkTargets(attackTip, 1.5f);
            Vector2 preferred = targets.Count > 0 ? (Vector2)targets[0].position : attackTip;
            if (TryFindNearest(loadout, data, origin, room, preferred, out Vector2 closest))
            {
                request = new PlacementPoint(closest, room != null ? room : world.FindRoomContaining(closest));
                return true;
            }
            int samples = Mathf.Max(1, GetCandidateSamples(loadout, data));
            for (int i = 0; i < samples; i++)
            {
                Vector2 candidate = i == 0 ? preferred : i == 1 ? attackTip : SampleFallbackCandidate(attackTip, 1.5f);
                if (!ValidateCandidate(loadout, data, origin, room, candidate)) continue;
                request = new PlacementPoint(candidate, room != null ? room : world.FindRoomContaining(candidate));
                return true;
            }
            request = default;
            return false;
        }
        finally
        {
            EndRequest();
        }
    }

    internal void BeginRequest(LightningSpearLoadout loadout, LightningSpearSkill2Data data,
        Vector2 origin, bool reuseGeometry = true)
    {
        pendingMarkSpawns.Clear();
        search.Begin(origin, GetFallbackCombatRadius(loadout, data), reuseGeometry);
    }

    internal void EndRequest()
    {
        search.End();
        world.ClearPlacementResults();
        markTargets.Clear();
        seenMarkTargets.Clear();
    }

    // Training dummies are damageable but deliberately do not inherit Enemy.
    // Keep this exception local to spear placement, not other relics' enemy/kill targeting.
    private List<Transform> FindMarkTargets(Vector2 center, float radius)
    {
        markTargets.Clear();
        seenMarkTargets.Clear();
        foreach (Collider2D hit in world.FindTargetHits(center, radius))
        {
            Transform target;
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                if (enemy.IsDead || !enemy.isActiveAndEnabled) continue;
                target = enemy.transform;
            }
            else
            {
                TrainingDummy2D dummy = hit.GetComponentInParent<TrainingDummy2D>();
                if (dummy == null || !dummy.isActiveAndEnabled) continue;
                target = dummy.transform;
            }
            if (seenMarkTargets.Add(target)) markTargets.Add(target);
        }
        markTargets.Sort((a, b) => ((Vector2)a.position - center).sqrMagnitude.CompareTo(
            ((Vector2)b.position - center).sqrMagnitude));
#if UNITY_EDITOR
        if (Counters != null) Counters.targetsFound = markTargets.Count;
#endif
        return markTargets;
    }

    internal bool TryFindNearest(LightningSpearLoadout loadout, LightningSpearSkill2Data data,
        Vector2 ownerPosition, MonsterRoomArea2D room, Vector2 preferred, out Vector2 result)
    {
        bool standalone = !search.IsActive;
        if (standalone)
            search.Begin(ownerPosition, GetFallbackCombatRadius(loadout, data));
        try
        {
            // Never snap a valid enemy position to a tile center: test its exact position first.
            if (ValidateCandidate(loadout, data, ownerPosition, room, preferred))
            {
                result = preferred;
                return true;
            }
            LightningSpearPlacementSearch.NearestCandidates nearest =
                search.GetNearest(preferred, world.ResolveGroundTilemaps());
            while (nearest.TryPeek(out Vector2 candidate))
            {
                if (ValidateCandidate(loadout, data, ownerPosition, room, candidate))
                {
                    // Keep the successful point until a reservation rejects it. This also
                    // preserves repeated selection when authored minimum spacing is zero.
                    result = candidate;
                    return true;
                }
                nearest.RejectCurrent();
            }
            search.RememberExhausted(preferred);
            result = default;
            return false;
        }
        finally
        {
            if (standalone)
                search.End();
        }
    }

    private bool ValidateCandidate(LightningSpearLoadout loadout, LightningSpearSkill2Data data,
        Vector2 ownerPosition, MonsterRoomArea2D ownerRoom, Vector2 candidate)
    {
#if UNITY_EDITOR
        if (Counters != null) Counters.candidateChecks++;
#endif
        if (!float.IsFinite(candidate.x) || !float.IsFinite(candidate.y))
            return false;
        if (Vector2.Distance(ownerPosition, candidate) > GetFallbackCombatRadius(loadout, data))
            return false;
        if (ownerRoom != null && !ownerRoom.Contains(candidate))
            return false;
        // Reject cheap, reservation-dependent conditions before tile/physics queries.
        if (Vector2.Distance(ownerPosition, candidate) < GetMinPlayerDistance(loadout, data))
            return false;
        if (!HasRequiredSpacing(loadout, data, candidate))
            return false;
        if (search.TryGetGeometry(candidate, out bool cachedValid))
            return cachedValid;
        // Wall tiles reject placement even when their collider only represents an outline.
        bool valid = !world.HasWallTileAt(candidate) &&
            !world.HasPlacementBlocker(ownerPosition, candidate, loadout.MarkRushBodyRadius, loadout.StrictRushBlockMask) &&
            world.IsLandingValid(loadout, data, candidate);
        search.RememberGeometry(candidate, valid);
        return valid;
    }

    private bool HasRequiredSpacing(LightningSpearLoadout loadout, LightningSpearSkill2Data data, Vector2 candidate)
    {
        float minSpacing = GetMinMarkSpacing(loadout, data);
        if (minSpacing <= 0f)
            return true;
        float minSpacingSqr = minSpacing * minSpacing;
        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark == null)
                continue;
            if (((Vector2)mark.transform.position - candidate).sqrMagnitude < minSpacingSqr)
                return false;
        }
        for (int i = 0; i < pendingMarkSpawns.Count; i++)
            if ((pendingMarkSpawns[i].position - candidate).sqrMagnitude < minSpacingSqr)
                return false;
        return true;
    }

    private static Vector2 SampleFallbackCandidate(Vector2 origin, float radius) =>
        origin + Random.insideUnitCircle * Mathf.Max(0.01f, radius);
    private static int GetMarkRainCount(LightningSpearLoadout loadout, LightningSpearSkill2Data data) =>
        data != null ? data.MarkRainCount : loadout.MarkRainCount;
    private static float GetFallbackCombatRadius(LightningSpearLoadout loadout, LightningSpearSkill2Data data) =>
        data != null ? data.FallbackCombatRadius : loadout.FallbackCombatRadius;
    private static float GetMinPlayerDistance(LightningSpearLoadout loadout, LightningSpearSkill2Data data) =>
        data != null ? data.MinPlayerDistance : loadout.MinPlayerDistance;
    private static float GetMinMarkSpacing(LightningSpearLoadout loadout, LightningSpearSkill2Data data) =>
        data != null ? data.MinMarkSpacing : loadout.MinMarkSpacing;
    private static int GetCandidateSamples(LightningSpearLoadout loadout, LightningSpearSkill2Data data) =>
        data != null ? data.CandidateSamples : loadout.CandidateSamples;

    internal void SetCounters(PlacementCounters counters) { Counters = counters; world.Counters = counters; }
    internal void InvalidateTilemapCache() => world.InvalidateTilemapCache();
    private void RemoveDestroyedMarks()
    {
        int write = 0;
        for (int read = 0; read < activeMarks.Count; read++)
            if (activeMarks[read] != null) activeMarks[write++] = activeMarks[read];
        if (write < activeMarks.Count) activeMarks.RemoveRange(write, activeMarks.Count - write);
    }
}

internal sealed class LightningSpearWorldBeforeTools
{
    private readonly List<Collider2D> markTargetHits = new List<Collider2D>();
    private readonly List<RaycastHit2D> placementCastHits = new List<RaycastHit2D>();
    private readonly List<Tilemap> groundTilemapCache = new List<Tilemap>();
    private readonly List<Tilemap> wallTilemapCache = new List<Tilemap>();
    private int groundTilemapCacheFrame = -1;
    private int cachedGroundLayer = int.MinValue;

#if UNITY_EDITOR
    internal PlacementCounters Counters;
#endif

    internal List<Collider2D> FindTargetHits(Vector2 center, float radius)
    {
        ContactFilter2D filter = new ContactFilter2D().NoFilter();
        filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        filter.useTriggers = Physics2D.queriesHitTriggers;
        // The list overload grows to include every hit; a full fixed buffer must not hide a target.
#if UNITY_EDITOR
        if (Counters != null) Counters.targetOverlapQueries++;
#endif
        Physics2D.OverlapCircle(center, radius, filter, markTargetHits);
        return markTargetHits;
    }

    internal void ClearPlacementResults()
    {
        markTargetHits.Clear();
        placementCastHits.Clear();
    }

    internal void InvalidateTilemapCache()
    {
        groundTilemapCacheFrame = -1;
    }

    internal bool IsLandingValid(LightningSpearLoadout loadout, LightningSpearSkill2Data data, Vector2 position)
    {
        if (loadout == null)
            return false;

        float radius = data != null ? data.LandingProbeRadius : loadout.LandingProbeRadius;

        int blockedMask = loadout.LandingBlockedMask;
#if UNITY_EDITOR
        if (Counters != null && blockedMask != 0) Counters.landingOverlapQueries++;
#endif
        if (blockedMask != 0 &&
            Physics2D.OverlapCircle(position, radius, blockedMask) != null)
        {
            return false;
        }

        return HasGroundTileAt(position) || HasRequiredGroundOverlap(loadout, position, radius);
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

    internal bool HasRequiredGroundOverlap(LightningSpearLoadout loadout, Vector2 position, float radius)
    {
        int requiredMask = loadout != null ? loadout.RequiredGroundLayers.value : 0;
        if (requiredMask == 0)
            return false;

#if UNITY_EDITOR
        if (Counters != null) Counters.landingOverlapQueries++;
#endif
        return Physics2D.OverlapCircle(position, radius, requiredMask) != null;
    }

    internal List<Tilemap> ResolveGroundTilemaps()
    {
        if (groundTilemapCacheFrame == Time.frameCount)
            return groundTilemapCache;

        groundTilemapCacheFrame = Time.frameCount;
        groundTilemapCache.Clear();
        wallTilemapCache.Clear();

        int groundLayer = ResolveGroundLayer();
        int wallLayer = LayerMask.NameToLayer("Wall");
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

            // Authored prototype Wall tilemaps also use the Ground physics layer.
            if (tilemap.gameObject.layer == wallLayer || tilemap.name == "Wall")
                wallTilemapCache.Add(tilemap);
            else if (tilemap.gameObject.layer == groundLayer)
                groundTilemapCache.Add(tilemap);
        }

        return groundTilemapCache;
    }

    private int ResolveGroundLayer()
    {
        if (cachedGroundLayer != int.MinValue)
            return cachedGroundLayer;

        int groundLayer = LayerMask.NameToLayer("Ground");
        cachedGroundLayer = groundLayer >= 0 ? groundLayer : 7;
        return cachedGroundLayer;
    }

    private static bool IsUsableGroundTilemap(Tilemap tilemap)
    {
        return tilemap != null && tilemap.isActiveAndEnabled && tilemap.gameObject.activeInHierarchy;
    }

    internal static bool HasBlocker(Vector2 start, Vector2 end, float radius, int layerMask)
    {
        if (layerMask == 0)
            return false;

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        Vector2 direction = delta / distance;
        if (radius > 0f)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(start, radius, direction, distance, layerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null)
                    continue;

                if (IsInitialPlacementOverlapMovingAway(start, direction, collider, hits[i]))
                    continue;

                return true;
            }

            return false;
        }

        return Physics2D.Linecast(start, end, layerMask).collider != null;
    }

    internal bool HasPlacementBlocker(Vector2 start, Vector2 end, float radius, int layerMask)
    {
        if (layerMask == 0)
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
            return Physics2D.Linecast(start, end, layerMask).collider != null;

        ContactFilter2D filter = new ContactFilter2D().NoFilter();
        filter.SetLayerMask(layerMask);
        filter.useTriggers = Physics2D.queriesHitTriggers;
        Physics2D.CircleCast(start, radius, direction, filter, placementCastHits, distance);
        for (int i = 0; i < placementCastHits.Count; i++)
        {
            RaycastHit2D hit = placementCastHits[i];
            Collider2D collider = hit.collider;
            if (collider == null)
                continue;

            if (IsInitialPlacementOverlapMovingAway(start, direction, collider, hit))
                continue;

            return true;
        }

        return false;
    }

    internal static bool IsInitialPlacementOverlapMovingAway(
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

}

#endif
