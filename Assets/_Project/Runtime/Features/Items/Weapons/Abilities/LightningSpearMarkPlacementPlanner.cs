using System.Collections.Generic;
using UnityEngine;

/// <summary>Live position access stays with the actor owner; common tools receive a value reader.</summary>
internal readonly struct LightningSpearMarkPositions : IPlacementPositions
{
    private readonly List<LightningSpearMarkActor> marks;
    internal LightningSpearMarkPositions(List<LightningSpearMarkActor> marks) => this.marks = marks;
    public int Count => marks.Count;
    public bool TryGetPosition(int index, out Vector2 position)
    {
        LightningSpearMarkActor mark = marks[index];
        if (mark == null) { position = default; return false; }
        position = mark.transform.position;
        return true;
    }
}

/// <summary>Converts weapon inputs and composes common placement operations. No actor creation.</summary>
internal sealed class LightningSpearMarkPlacementPlanner
{
    private static readonly PlacementTargetResolver resolveTarget = TryResolveTarget;
    private readonly LightningSpearPlacementWorldQuery world;
    private readonly CandidateSources sources = new CandidateSources();
    private readonly NearestPositionSearch search;
    private readonly PlacementSession<LightningSpearMarkPositions> session;
    internal NearestPositionSearch Search => search;
    internal PlacementSession<LightningSpearMarkPositions> Session => session;
    internal List<PlacementPoint> Pending => session.Pending;
#if UNITY_EDITOR
    internal PlacementCounters Counters
    {
        set { session.Counters = value; sources.Counters = value; world.Counters = value; }
    }
#endif
    internal LightningSpearMarkPlacementPlanner(LightningSpearPlacementWorldQuery world,
        List<LightningSpearMarkActor> activeMarks)
    {
        this.world = world;
        search = new NearestPositionSearch(sources);
        session = new PlacementSession<LightningSpearMarkPositions>(world.Maps, world.Physics,
            new LightningSpearMarkPositions(activeMarks));
    }

    /// <summary>Borrowed until the next request. Copy before a coroutine yields.</summary>
    internal List<PlacementPoint> GenerateRain(LightningSpearLoadout loadout,
        LightningSpearSkill2Data data, Vector2 origin, bool targeted)
    {
        int count = data != null ? data.MarkRainCount : loadout.MarkRainCount;
        BeginRequest(loadout, data, origin, targeted, count > 0);
        try
        {
            if (count <= 0) return Pending;
            float radius = data != null ? data.FallbackCombatRadius : loadout.FallbackCombatRadius;
            if (targeted)
                PlacementFlow.RoundRobinFill(sources.FindTargets(world.Physics, origin, radius, resolveTarget),
                    count, search, world.Maps, session);
            // The same bounded fill supplies ordinary rain and the targeted program's remainder.
            var random = CandidateSources.RandomDisk(origin, radius);
            int samples = data != null ? data.CandidateSamples : loadout.CandidateSamples;
            PlacementFlow.FillWithinBudget(ref random, Mathf.Max(samples, count * 12), count, session);
            return Pending;
        }
        finally { EndRequest(); }
    }

    internal bool TryPlanThirdStrike(LightningSpearLoadout loadout, LightningSpearSkill2Data data,
        Vector2 origin, Vector2 attackTip, out PlacementPoint point)
    {
        BeginRequest(loadout, data, origin);
        try
        {
            List<Transform> targets = sources.FindTargets(world.Physics, attackTip, 1.5f, resolveTarget);
            Vector2 preferred = targets.Count > 0 ? (Vector2)targets[0].position : attackTip;
            var fallback = CandidateSources.FixedThenRandom(preferred, attackTip, attackTip, 1.5f);
            int samples = data != null ? data.CandidateSamples : loadout.CandidateSamples;
            return PlacementFlow.FirstSuccess(preferred, search, world.Maps, ref fallback,
                Mathf.Max(1, samples), session, out point);
        }
        finally
        {
            EndRequest();
            // A single value has been transferred to the owner; there is no borrowed batch to retain.
            Pending.Clear();
        }
    }

    internal void BeginRequest(LightningSpearLoadout loadout, LightningSpearSkill2Data data,
        Vector2 origin, bool reuseGeometry = true, bool resolveRegion = true)
    {
        PlacementSettings settings = CreateSettings(loadout, data, origin,
            resolveRegion ? world.FindRoomContaining(origin) : null, reuseGeometry);
        session.Begin(settings);
        search.Begin(origin, settings.range);
    }
    internal void EndRequest()
    {
        session.End();
        search.End();
        world.ClearPlacementResults();
    }
    internal bool TryFindNearest(LightningSpearLoadout loadout, LightningSpearSkill2Data data,
        Vector2 origin, MonsterRoomArea2D room, Vector2 preferred, out Vector2 result)
    {
        bool standalone = !search.IsActive;
        if (standalone)
        {
            PlacementSettings settings = CreateSettings(loadout, data, origin, room, true);
            session.Begin(settings, false);
            search.Begin(origin, settings.range);
        }
        try { return search.TryFind(preferred, world.Maps, session, out result); }
        finally { if (standalone) EndRequest(); }
    }
    // Source compatibility for the existing Editor nearest/spacing probe.
    private bool ValidateCandidate(LightningSpearLoadout loadout, LightningSpearSkill2Data data,
        Vector2 origin, MonsterRoomArea2D room, Vector2 candidate) => session.CanPlace(candidate);

    private static PlacementSettings CreateSettings(LightningSpearLoadout loadout,
        LightningSpearSkill2Data data, Vector2 origin, MonsterRoomArea2D room, bool reuseGeometry) =>
        new PlacementSettings(origin,
            data != null ? data.FallbackCombatRadius : loadout.FallbackCombatRadius, room,
            data != null ? data.MinPlayerDistance : loadout.MinPlayerDistance,
            data != null ? data.MinMarkSpacing : loadout.MinMarkSpacing,
            loadout.MarkRushBodyRadius, loadout.StrictRushBlockMask,
            data != null ? data.LandingProbeRadius : loadout.LandingProbeRadius,
            loadout.LandingBlockedMask, loadout.RequiredGroundLayers.value,
            true, true, true, reuseGeometry);

    private static bool TryResolveTarget(Collider2D hit, out Transform target)
    {
        Enemy enemy = hit.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            if (enemy.IsDead || !enemy.isActiveAndEnabled) { target = null; return false; }
            target = enemy.transform;
            return true;
        }
        // Dummies belong to this content's inclusion rule, not every combat target query.
        TrainingDummy2D dummy = hit.GetComponentInParent<TrainingDummy2D>();
        target = dummy != null && dummy.isActiveAndEnabled ? dummy.transform : null;
        return target != null;
    }
}
