#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

// Measurement-only paths. Excluded from player builds and never selected by gameplay.
public sealed partial class LightningSpearRuntimeState
{
    private readonly List<PlacementPoint> baselineMarkSpawns = new List<PlacementPoint>();

    [Serializable]
    private sealed class PlacementBenchmarkSample
    {
        public int seed;
        public bool equivalent;
        public bool optimizedValid;
        public bool rngEquivalent;
        public bool workEquivalent;
        public int baselineCount;
        public int optimizedCount;
        public double baselineMilliseconds;
        public double optimizedMilliseconds;
        public long baselineAllocatedBytes;
        public long optimizedAllocatedBytes;
        public PlacementCounters baseline;
        public PlacementCounters optimized;
        public Vector2[] baselinePositions;
        public Vector2[] optimizedPositions;
        public int[] baselineRoomIds;
        public int[] optimizedRoomIds;
    }

    [Serializable]
    private sealed class PlacementBenchmarkReport
    {
        public string baselineCommit = "80b48541fe46ad19eb06d2c74d7cc13a22916777";
        public string baselineKind = "legacy-before-algorithm-optimization";
        public string baselineSourceSha256;
        public string unityVersion;
        public string graphicsDevice;
        public string pattern;
        public string scene;
        public bool playMode;
        public bool targeted;
        public Vector2 origin;
        public int activeMarks;
        public int iterations;
        public int timingRepetitions;
        public bool allEquivalent = true;
        public bool allOptimizedValid = true;
        public bool allRngEquivalent = true;
        public bool allWorkEquivalent = true;
        public PlacementBenchmarkSample firstRequest;
        public PlacementBenchmarkSample[] freshBufferSamples;
        public int[] freshBufferGcCollections;
        public bool managedAllocationCounterAvailable;
        public long allocationCanaryBytes;
        public string allocationMeasurement;
        public int[] gcCollections;
        public PlacementBenchmarkSample[] samples;
    }

    private PlacementCounters placementBenchmarkCounters;
    private bool placementAllocationCounterAvailable;
    private Random.State baselineRandomState;
    private Random.State optimizedRandomState;
    private LightningSpearPlacementBeforeTools structureBaseline;

    internal string RunStructureBenchmarkWithLoadout(LightningSpearLoadout loadout,
        LightningSpearSkill2Data data, Vector2 origin, int seed, int iterations, bool targeted)
    {
        structureBaseline = new LightningSpearPlacementBeforeTools(activeMarks);
        try { return RunPlacementBenchmarkWithLoadout(loadout, data, origin, seed, iterations, targeted); }
        finally { structureBaseline = null; }
    }

    internal string RunPlacementBenchmark(int seed, int iterations, bool targeted)
    {
        if (!TryResolveActiveLoadout(out LightningSpearLoadout loadout))
            throw new InvalidOperationException("Equip Lightning Spear before running the placement benchmark.");
        return RunPlacementBenchmarkWithLoadout(loadout, ResolveSkill2Data(loadout),
            ownerSystem.transform.position, seed, iterations, targeted);
    }

    internal string RunThirdStrikeBenchmarkWithLoadout(LightningSpearLoadout loadout,
        LightningSpearSkill2Data data, Vector2 origin, int seed, int iterations)
    {
        Random.State savedRandom = Random.state;
        var savedPending = new List<PlacementPoint>(PlacementPlanner.Pending);
        var before = new LightningSpearPlacementBeforeTools(activeMarks);
        var report = new PlacementBenchmarkReport
        {
            baselineKind = "weapon-only-planner-before-tool-composition",
            baselineCommit = null,
            baselineSourceSha256 = "16299d7f32811b84eb0c6d63cdea25681a91fc32e7b123735f4485e4fbfb45bc",
            unityVersion = Application.unityVersion,
            graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
            pattern = "third-strike",
            scene = gameObject.scene.name,
            playMode = Application.isPlaying,
            origin = origin,
            iterations = iterations,
            timingRepetitions = 9,
            allocationMeasurement = "Not sampled by the third-strike probe",
            samples = new PlacementBenchmarkSample[iterations]
        };
        try
        {
            for (int warmup = 0; warmup < 8; warmup++)
            {
                var discarded = new PlacementBenchmarkSample { seed = seed + warmup };
                MeasureThirdStrike(false, discarded, before, loadout, data, origin, origin + Vector2.right * 3f);
                MeasureThirdStrike(true, discarded, before, loadout, data, origin, origin + Vector2.right * 3f);
            }
            for (int i = 0; i < iterations; i++)
            {
                var sample = new PlacementBenchmarkSample { seed = seed + i };
                Vector2 tip = origin + new Vector2((i % 13) - 6.1f, (i % 5) - 2.2f);
                MeasureThirdStrike((i & 1) != 0, sample, before, loadout, data, origin, tip);
                MeasureThirdStrike((i & 1) == 0, sample, before, loadout, data, origin, tip);
                sample.equivalent = sample.baselineCount == sample.optimizedCount;
                if (sample.equivalent && sample.baselineCount > 0)
                    sample.equivalent = sample.baselinePositions[0].Equals(sample.optimizedPositions[0]) &&
                        sample.baselineRoomIds[0] == sample.optimizedRoomIds[0];
                sample.rngEquivalent = baselineRandomState.Equals(optimizedRandomState);
                sample.workEquivalent = JsonUtility.ToJson(sample.baseline) == JsonUtility.ToJson(sample.optimized);
                sample.optimizedValid = ValidateBenchmarkPositions(loadout, data, origin, sample.optimizedPositions);
                report.allEquivalent &= sample.equivalent;
                report.allRngEquivalent &= sample.rngEquivalent;
                report.allWorkEquivalent &= sample.workEquivalent;
                report.allOptimizedValid &= sample.optimizedValid;
                report.samples[i] = sample;
            }
            return JsonUtility.ToJson(report, true);
        }
        finally
        {
            PlacementPlanner.Counters = null;
            before.SetCounters(null);
            PlacementPlanner.EndRequest();
            PlacementPlanner.Pending.Clear();
            PlacementPlanner.Pending.AddRange(savedPending);
            Random.state = savedRandom;
        }
    }

    private void MeasureThirdStrike(bool optimized, PlacementBenchmarkSample sample,
        LightningSpearPlacementBeforeTools before, LightningSpearLoadout loadout,
        LightningSpearSkill2Data data, Vector2 origin, Vector2 tip)
    {
        PlacementPoint point = default;
        bool found = false;
        PlacementCounters counters = null;
        long ticks = 0;
        for (int repeat = 0; repeat < 9; repeat++)
        {
            Random.InitState(sample.seed);
            counters = new PlacementCounters();
            PlacementPlanner.Counters = counters;
            before.SetCounters(counters);
            placementWorld.InvalidateTilemapCache();
            before.InvalidateTilemapCache();
            long started = Stopwatch.GetTimestamp();
            RemoveDestroyedMarks();
            found = optimized
                ? PlacementPlanner.TryPlanThirdStrike(loadout, data, origin, tip, out point)
                : before.TryPlanThirdStrike(loadout, data, origin, tip, out point);
            ticks += Stopwatch.GetTimestamp() - started;
        }
        counters.candidateCollections = optimized ? PlacementPlanner.Search.CollectionCount : before.Search.CollectionCount;
        counters.visitedCells = optimized ? PlacementPlanner.Search.VisitedCells : before.Search.VisitedCells;
        counters.heapBuilds = optimized ? PlacementPlanner.Search.HeapBuildCount : before.Search.HeapBuildCount;
        counters.geometryCacheHits = optimized ? PlacementPlanner.Session.GeometryCacheHits : before.Search.GeometryCacheHits;
        Vector2[] points = found ? new[] { point.position } : Array.Empty<Vector2>();
        int[] rooms = found ? new[] { point.room != null ? point.room.GetInstanceID() : 0 } : Array.Empty<int>();
        double milliseconds = ticks * 1000d / Stopwatch.Frequency / 9;
        if (optimized)
        {
            optimizedRandomState = Random.state;
            sample.optimized = counters;
            sample.optimizedCount = points.Length;
            sample.optimizedPositions = points;
            sample.optimizedRoomIds = rooms;
            sample.optimizedMilliseconds = milliseconds;
            sample.optimizedAllocatedBytes = -1;
        }
        else
        {
            baselineRandomState = Random.state;
            sample.baseline = counters;
            sample.baselineCount = points.Length;
            sample.baselinePositions = points;
            sample.baselineRoomIds = rooms;
            sample.baselineMilliseconds = milliseconds;
            sample.baselineAllocatedBytes = -1;
        }
    }

    internal string RunPlacementBenchmarkWithLoadout(LightningSpearLoadout loadout,
        LightningSpearSkill2Data data, Vector2 origin, int seed, int iterations, bool targeted)
    {
        if (PlacementPlanner.Search.IsActive || placementBenchmarkCounters != null)
            throw new InvalidOperationException("A placement request or benchmark is already running.");
        iterations = Mathf.Clamp(iterations, 1, 1000);
        Random.State randomState = Random.state;
        var savedPending = new List<PlacementPoint>(PlacementPlanner.Pending);
        var savedMarks = new List<LightningSpearMarkActor>(activeMarks);
        long canaryBefore = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(new byte[4096]);
        long canaryBytes = GC.GetAllocatedBytesForCurrentThread() - canaryBefore;
        placementAllocationCounterAvailable = canaryBytes >= 4096;
        var report = new PlacementBenchmarkReport
        {
            baselineKind = structureBaseline != null ? "weapon-only-planner-before-tool-composition" : "legacy-before-algorithm-optimization",
            baselineCommit = structureBaseline != null ? null : "80b48541fe46ad19eb06d2c74d7cc13a22916777",
            baselineSourceSha256 = structureBaseline != null ? "16299d7f32811b84eb0c6d63cdea25681a91fc32e7b123735f4485e4fbfb45bc" : null,
            unityVersion = Application.unityVersion,
            graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
            scene = gameObject.scene.name,
            playMode = Application.isPlaying,
            targeted = targeted,
            origin = origin,
            activeMarks = activeMarks.Count,
            iterations = iterations,
            timingRepetitions = structureBaseline != null ? 9 : 1,
            managedAllocationCounterAvailable = placementAllocationCounterAvailable,
            allocationCanaryBytes = canaryBytes,
            allocationMeasurement = placementAllocationCounterAvailable
                ? "GC.GetAllocatedBytesForCurrentThread; managed allocation on query thread"
                : "Unavailable: 4096-byte allocation canary was not observed; byte samples are -1",
            samples = new PlacementBenchmarkSample[iterations]
        };
        try
        {
            Physics2D.SyncTransforms();
            if (structureBaseline != null)
            {
                // One pre-warmup probe; a reused runtime may already have populated buffers.
                // This does not establish a repeatable cold/JIT cost.
                var first = new PlacementBenchmarkSample { seed = seed };
                MeasurePlacement(false, first, loadout, data, origin, targeted, 1);
                MeasurePlacement(true, first, loadout, data, origin, targeted, 1);
                CheckPlacementSample(first, loadout, data, origin);
                report.firstRequest = first;
                report.allEquivalent &= first.equivalent;
                report.allOptimizedValid &= first.optimizedValid;
                report.allRngEquivalent &= first.rngEquivalent;
                report.allWorkEquivalent &= first.workEquivalent;
            }
            // Warm both paths and reusable buffers before sampling. Random is restored per pair.
            for (int warmup = 0; warmup < 8; warmup++)
            {
                Random.InitState(seed + warmup);
                GenerateBenchmarkBaseline(loadout, data, origin, targeted);
                Random.InitState(seed + warmup);
                GenerateMarkPositions(loadout, data, origin, targeted);
            }
            if (structureBaseline != null)
            {
                // Warm constructor/first-request code separately. These probes measure fresh
                // managed buffers after JIT/Unity initialization, not process or MonoBehaviour startup.
                for (int warmup = 0; warmup < 2; warmup++)
                {
                    var discarded = new PlacementBenchmarkSample { seed = seed + warmup };
                    if ((warmup & 1) == 0)
                    {
                        MeasureFresh(false, discarded, loadout, data, origin, targeted);
                        MeasureFresh(true, discarded, loadout, data, origin, targeted);
                    }
                    else
                    {
                        MeasureFresh(true, discarded, loadout, data, origin, targeted);
                        MeasureFresh(false, discarded, loadout, data, origin, targeted);
                    }
                    CheckPlacementSample(discarded, loadout, data, origin);
                    report.allEquivalent &= discarded.equivalent;
                    report.allOptimizedValid &= discarded.optimizedValid;
                    report.allRngEquivalent &= discarded.rngEquivalent;
                    report.allWorkEquivalent &= discarded.workEquivalent;
                }
                report.freshBufferSamples = new PlacementBenchmarkSample[24];
                int[] freshGcStart = { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
                for (int iteration = 0; iteration < report.freshBufferSamples.Length; iteration++)
                {
                    var sample = new PlacementBenchmarkSample { seed = seed + iteration };
                    if ((iteration & 1) == 0)
                    {
                        MeasureFresh(false, sample, loadout, data, origin, targeted);
                        MeasureFresh(true, sample, loadout, data, origin, targeted);
                    }
                    else
                    {
                        MeasureFresh(true, sample, loadout, data, origin, targeted);
                        MeasureFresh(false, sample, loadout, data, origin, targeted);
                    }
                    CheckPlacementSample(sample, loadout, data, origin);
                    report.allEquivalent &= sample.equivalent;
                    report.allOptimizedValid &= sample.optimizedValid;
                    report.allRngEquivalent &= sample.rngEquivalent;
                    report.allWorkEquivalent &= sample.workEquivalent;
                    report.freshBufferSamples[iteration] = sample;
                }
                report.freshBufferGcCollections = new[] { GC.CollectionCount(0) - freshGcStart[0],
                    GC.CollectionCount(1) - freshGcStart[1], GC.CollectionCount(2) - freshGcStart[2] };
            }
            int[] gcStart = { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var sample = new PlacementBenchmarkSample { seed = seed + iteration };
                // Alternate order to reduce systematic first-run/cache bias.
                if ((iteration & 1) == 0)
                {
                    MeasurePlacement(false, sample, loadout, data, origin, targeted);
                    MeasurePlacement(true, sample, loadout, data, origin, targeted);
                }
                else
                {
                    MeasurePlacement(true, sample, loadout, data, origin, targeted);
                    MeasurePlacement(false, sample, loadout, data, origin, targeted);
                }
                CheckPlacementSample(sample, loadout, data, origin);
                report.allEquivalent &= sample.equivalent;
                report.allOptimizedValid &= sample.optimizedValid;
                report.allRngEquivalent &= sample.rngEquivalent;
                report.allWorkEquivalent &= sample.workEquivalent;
                report.samples[iteration] = sample;
            }
            report.gcCollections = new[] { GC.CollectionCount(0) - gcStart[0],
                GC.CollectionCount(1) - gcStart[1], GC.CollectionCount(2) - gcStart[2] };
            return JsonUtility.ToJson(report, true);
        }
        finally
        {
            placementBenchmarkCounters = null;
            placementAllocationCounterAvailable = false;
            PlacementPlanner.EndRequest();
            PlacementPlanner.Counters = null;
            placementWorld.Counters = null;
            baselineMarkSpawns.Clear();
            PlacementPlanner.Pending.Clear();
            PlacementPlanner.Pending.AddRange(savedPending);
            activeMarks.Clear();
            activeMarks.AddRange(savedMarks);
            Random.state = randomState;
        }
    }

    private List<PlacementPoint> GenerateBenchmarkBaseline(LightningSpearLoadout loadout,
        LightningSpearSkill2Data data, Vector2 origin, bool targeted) => structureBaseline != null
        ? structureBaseline.GenerateMarkPositions(loadout, data, origin, targeted)
        : GenerateMarkPositionsBaseline(loadout, data, origin, targeted);

    private void CheckPlacementSample(PlacementBenchmarkSample sample, LightningSpearLoadout loadout,
        LightningSpearSkill2Data data, Vector2 origin)
    {
        sample.equivalent = sample.baselineCount == sample.optimizedCount;
        for (int i = 0; sample.equivalent && i < sample.baselineCount; i++)
            sample.equivalent = sample.baselinePositions[i].Equals(sample.optimizedPositions[i]) &&
                sample.baselineRoomIds[i] == sample.optimizedRoomIds[i];
        sample.optimizedValid = ValidateBenchmarkPositions(loadout, data, origin, sample.optimizedPositions);
        sample.rngEquivalent = baselineRandomState.Equals(optimizedRandomState);
        // Legacy optimization changes work deliberately; structure comparison must preserve every counter.
        sample.workEquivalent = structureBaseline == null || JsonUtility.ToJson(sample.baseline) == JsonUtility.ToJson(sample.optimized);
    }

    private void MeasurePlacement(bool optimized, PlacementBenchmarkSample sample,
        LightningSpearLoadout loadout, LightningSpearSkill2Data data, Vector2 origin, bool targeted,
        int repetitions = 0)
    {
        repetitions = repetitions > 0 ? repetitions : structureBaseline != null ? 9 : 1;
        PlacementCounters counters = null;
        List<PlacementPoint> result = null;
        long elapsedTicks = 0;
        long allocatedTotal = 0;
        // Repeat a seed to reduce scheduler noise. Counters/output describe the last
        // identical request; RNG setup, counter allocation and cache invalidation stay outside timing.
        for (int repeat = 0; repeat < repetitions; repeat++)
        {
            Random.InitState(sample.seed);
            counters = new PlacementCounters();
            placementBenchmarkCounters = counters;
            PlacementPlanner.Counters = counters;
            placementWorld.Counters = counters;
            // Include the same one-per-request tilemap discovery cost in both paths.
            placementWorld.InvalidateTilemapCache();
            if (structureBaseline != null)
            {
                structureBaseline.SetCounters(counters);
                structureBaseline.InvalidateTilemapCache();
            }
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            result = optimized
                ? GenerateMarkPositions(loadout, data, origin, targeted)
                : GenerateBenchmarkBaseline(loadout, data, origin, targeted);
            elapsedTicks += Stopwatch.GetTimestamp() - started;
            allocatedTotal += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        }
        double elapsed = elapsedTicks * 1000d / Stopwatch.Frequency / repetitions;
        long allocated = placementAllocationCounterAvailable
            ? allocatedTotal / repetitions : -1;
        placementBenchmarkCounters = null;
        PlacementPlanner.Counters = null;
        placementWorld.Counters = null;
        if (structureBaseline != null) structureBaseline.SetCounters(null);
        Vector2[] positions = new Vector2[result.Count];
        int[] roomIds = new int[result.Count];
        for (int i = 0; i < result.Count; i++)
        {
            positions[i] = result[i].position;
            roomIds[i] = result[i].room != null ? result[i].room.GetInstanceID() : 0;
        }
        if (optimized)
        {
            optimizedRandomState = Random.state;
            counters.candidateCollections = PlacementPlanner.Search.CollectionCount;
            counters.visitedCells = PlacementPlanner.Search.VisitedCells;
            counters.heapBuilds = PlacementPlanner.Search.HeapBuildCount;
            counters.geometryCacheHits = PlacementPlanner.Session.GeometryCacheHits;
            sample.optimized = counters;
            sample.optimizedCount = positions.Length;
            sample.optimizedPositions = positions;
            sample.optimizedRoomIds = roomIds;
            sample.optimizedMilliseconds = elapsed;
            sample.optimizedAllocatedBytes = allocated;
        }
        else
        {
            baselineRandomState = Random.state;
            if (structureBaseline != null)
            {
                counters.candidateCollections = structureBaseline.Search.CollectionCount;
                counters.visitedCells = structureBaseline.Search.VisitedCells;
                counters.heapBuilds = structureBaseline.Search.HeapBuildCount;
                counters.geometryCacheHits = structureBaseline.Search.GeometryCacheHits;
            }
            sample.baseline = counters;
            sample.baselineCount = positions.Length;
            sample.baselinePositions = positions;
            sample.baselineRoomIds = roomIds;
            sample.baselineMilliseconds = elapsed;
            sample.baselineAllocatedBytes = allocated;
        }
    }

    private void MeasureFresh(bool optimized, PlacementBenchmarkSample sample,
        LightningSpearLoadout loadout, LightningSpearSkill2Data data, Vector2 origin, bool targeted)
    {
        Random.InitState(sample.seed);
        var counters = new PlacementCounters();
        LightningSpearPlacementBeforeTools before = null;
        LightningSpearMarkPlacementPlanner planner = null;
        List<PlacementPoint> result;
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        if (optimized)
        {
            RemoveDestroyedMarks();
            var world = new LightningSpearPlacementWorldQuery();
            planner = new LightningSpearMarkPlacementPlanner(world, activeMarks);
            world.Counters = counters;
            planner.Counters = counters;
            result = planner.GenerateRain(loadout, data, origin, targeted);
        }
        else
        {
            before = new LightningSpearPlacementBeforeTools(activeMarks);
            before.SetCounters(counters);
            result = before.GenerateMarkPositions(loadout, data, origin, targeted);
        }
        double elapsed = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
        long allocated = placementAllocationCounterAvailable
            ? GC.GetAllocatedBytesForCurrentThread() - allocatedBefore : -1;
        counters.candidateCollections = optimized ? planner.Search.CollectionCount : before.Search.CollectionCount;
        counters.visitedCells = optimized ? planner.Search.VisitedCells : before.Search.VisitedCells;
        counters.heapBuilds = optimized ? planner.Search.HeapBuildCount : before.Search.HeapBuildCount;
        counters.geometryCacheHits = optimized ? planner.Session.GeometryCacheHits : before.Search.GeometryCacheHits;
        Vector2[] positions = new Vector2[result.Count];
        int[] roomIds = new int[result.Count];
        for (int i = 0; i < result.Count; i++)
        {
            positions[i] = result[i].position;
            roomIds[i] = result[i].room != null ? result[i].room.GetInstanceID() : 0;
        }
        if (optimized)
        {
            optimizedRandomState = Random.state;
            sample.optimized = counters;
            sample.optimizedCount = positions.Length;
            sample.optimizedPositions = positions;
            sample.optimizedRoomIds = roomIds;
            sample.optimizedMilliseconds = elapsed;
            sample.optimizedAllocatedBytes = allocated;
        }
        else
        {
            baselineRandomState = Random.state;
            sample.baseline = counters;
            sample.baselineCount = positions.Length;
            sample.baselinePositions = positions;
            sample.baselineRoomIds = roomIds;
            sample.baselineMilliseconds = elapsed;
            sample.baselineAllocatedBytes = allocated;
        }
    }

    private bool ValidateBenchmarkPositions(LightningSpearLoadout loadout,
        LightningSpearSkill2Data data, Vector2 origin, Vector2[] positions)
    {
        baselineMarkSpawns.Clear();
        MonsterRoomArea2D room = placementWorld.FindRoomContaining(origin);
        foreach (Vector2 position in positions)
        {
            // Independently validate the new result through the original rules.
            if (!ValidateMarkCandidateBaseline(loadout, data, origin, room, position))
                return false;
            baselineMarkSpawns.Add(new PlacementPoint(position, room));
        }
        return true;
    }

    private static Vector2 SampleFallbackCandidate(Vector2 origin, float radius) =>
        origin + Random.insideUnitCircle * Mathf.Max(0.01f, radius);

    // Editor fixture entry; gameplay calls the planner directly.
    private bool TryFindNearestMarkPosition(LightningSpearLoadout loadout, LightningSpearSkill2Data data,
        Vector2 origin, MonsterRoomArea2D room, Vector2 preferred, out Vector2 result)
    {
        RemoveDestroyedMarks();
        return PlacementPlanner.TryFindNearest(loadout, data, origin, room, preferred, out result);
    }

    private List<Tilemap> ResolveGroundTilemaps() => placementWorld.ResolveGroundTilemaps();

    // The following methods retain the pre-optimization placement rules independently of the planner.
    private List<PlacementPoint> GenerateMarkPositionsBaseline(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data,
        Vector2 ownerPosition, bool targeted)
    {
        baselineMarkSpawns.Clear();

        int targetCount = GetMarkRainCount(loadout, data);
        if (targetCount <= 0)
            return baselineMarkSpawns;

        MonsterRoomArea2D ownerRoom = placementWorld.FindRoomContaining(ownerPosition);
        if (targeted)
        {
            List<Transform> targets = FindMarkTargetsBaseline(ownerPosition, GetFallbackCombatRadius(loadout, data));
            // Give each enemy a direct strike first, then reserve the nearest free tiles around them.
            while (baselineMarkSpawns.Count < targetCount)
            {
                int previousCount = baselineMarkSpawns.Count;
                foreach (Transform target in targets)
                {
                    if (target == null || !TryFindNearestMarkPositionBaseline(loadout, data, ownerPosition,
                        ownerRoom, target.position, out Vector2 candidate)) continue;
                    baselineMarkSpawns.Add(new PlacementPoint(candidate, ownerRoom != null ? ownerRoom : placementWorld.FindRoomContaining(candidate)));
                    if (baselineMarkSpawns.Count >= targetCount) return baselineMarkSpawns;
                }
                if (baselineMarkSpawns.Count == previousCount) break;
            }
        }
        int sampleCount = Mathf.Max(GetCandidateSamples(loadout, data), targetCount * 12);
        for (int i = 0; i < sampleCount && baselineMarkSpawns.Count < targetCount; i++)
        {
            Vector2 candidate = SampleFallbackCandidate(ownerPosition, GetFallbackCombatRadius(loadout, data));

            if (!ValidateMarkCandidateBaseline(loadout, data, ownerPosition, ownerRoom, candidate))
                continue;

            MonsterRoomArea2D markRoom = ownerRoom != null ? ownerRoom : placementWorld.FindRoomContaining(candidate);
            baselineMarkSpawns.Add(new PlacementPoint(candidate, markRoom));
        }

        return baselineMarkSpawns;
    }

    private List<Transform> FindMarkTargetsBaseline(Vector2 center, float radius)
    {
        if (placementBenchmarkCounters != null) placementBenchmarkCounters.targetOverlapQueries++;
        var targets = new List<Transform>();
        var seen = new HashSet<Transform>();
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, radius))
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
            if (seen.Add(target)) targets.Add(target);
        }
        targets.Sort((a, b) => ((Vector2)a.position - center).sqrMagnitude.CompareTo(
            ((Vector2)b.position - center).sqrMagnitude));
        if (placementBenchmarkCounters != null) placementBenchmarkCounters.targetsFound = targets.Count;
        return targets;
    }

    private bool TryFindNearestMarkPositionBaseline(LightningSpearLoadout loadout, LightningSpearSkill2Data data,
        Vector2 ownerPosition, MonsterRoomArea2D room, Vector2 preferred, out Vector2 result)
    {
        // Never snap a valid enemy position to a tile center: the first choice is directly beneath it.
        if (ValidateMarkCandidateBaseline(loadout, data, ownerPosition, room, preferred))
        {
            result = preferred;
            return true;
        }

        float range = GetFallbackCombatRadius(loadout, data);
        float rangeSquared = range * range;
        if (placementBenchmarkCounters != null) placementBenchmarkCounters.candidateCollections++;
        var candidates = new List<Vector2>();
        foreach (Tilemap tilemap in placementWorld.ResolveGroundTilemaps())
        {
            // Bound enumeration to casting range, not the entire dungeon floor tilemap.
            Vector3Int lower = tilemap.WorldToCell(ownerPosition - Vector2.one * range);
            Vector3Int upper = lower;
            for (int corner = 1; corner < 4; corner++)
            {
                Vector2 offset = new Vector2((corner & 1) == 0 ? -range : range, (corner & 2) == 0 ? -range : range);
                Vector3Int cell = tilemap.WorldToCell(ownerPosition + offset);
                lower = Vector3Int.Min(lower, cell);
                upper = Vector3Int.Max(upper, cell);
            }
            BoundsInt bounds = tilemap.cellBounds;
            lower = Vector3Int.Max(bounds.min, lower);
            upper = Vector3Int.Min(bounds.max, upper + Vector3Int.one);
            if (lower.x >= upper.x || lower.y >= upper.y || lower.z >= upper.z) continue;
            bounds.SetMinMax(lower, upper);
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (placementBenchmarkCounters != null) placementBenchmarkCounters.visitedCells++;
                Vector2 point = tilemap.GetCellCenterWorld(cell);
                if ((point - ownerPosition).sqrMagnitude <= rangeSquared && tilemap.HasTile(cell))
                    candidates.Add(point);
            }
        }
        if (placementBenchmarkCounters != null) placementBenchmarkCounters.fullSorts++;
        candidates.Sort((a, b) =>
        {
            int distance = (a - preferred).sqrMagnitude.CompareTo((b - preferred).sqrMagnitude);
            if (distance != 0) return distance;
            int x = a.x.CompareTo(b.x);
            return x != 0 ? x : a.y.CompareTo(b.y);
        });
        foreach (Vector2 candidate in candidates)
        {
            if (!ValidateMarkCandidateBaseline(loadout, data, ownerPosition, room, candidate)) continue;
            result = candidate;
            return true;
        }
        result = default;
        return false;
    }

    private bool ValidateMarkCandidateBaseline(
        LightningSpearLoadout loadout,
        LightningSpearSkill2Data data,
        Vector2 ownerPosition,
        MonsterRoomArea2D ownerRoom,
        Vector2 candidate)
    {
        if (placementBenchmarkCounters != null) placementBenchmarkCounters.candidateChecks++;
        if (!float.IsFinite(candidate.x) || !float.IsFinite(candidate.y))
            return false;

        if (Vector2.Distance(ownerPosition, candidate) > GetFallbackCombatRadius(loadout, data))
            return false;

        if (ownerRoom != null && !ownerRoom.Contains(candidate))
            return false;

        // Wall tiles reject placement even when their collider only represents an outline.
        if (placementWorld.HasWallTileAt(candidate))
            return false;

        if (HasPlacementBlockerBaseline(ownerPosition, candidate, loadout.MarkRushBodyRadius, loadout.StrictRushBlockMask))
            return false;

        if (Vector2.Distance(ownerPosition, candidate) < GetMinPlayerDistance(loadout, data))
            return false;

        if (!placementWorld.IsLandingValid(loadout, data, candidate))
            return false;

        if (!HasRequiredSpacingBaseline(loadout, data, candidate))
            return false;

        return true;
    }

    private bool HasRequiredSpacingBaseline(LightningSpearLoadout loadout, LightningSpearSkill2Data data, Vector2 candidate)
    {
        float minSpacing = GetMinMarkSpacing(loadout, data);
        if (minSpacing <= 0f)
            return true;

        float minSpacingSqr = minSpacing * minSpacing;
        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            LightningSpearMarkActor mark = activeMarks[i];
            if (mark == null)
            {
                activeMarks.RemoveAt(i);
                continue;
            }

            if (((Vector2)mark.transform.position - candidate).sqrMagnitude < minSpacingSqr)
                return false;
        }

        for (int i = 0; i < baselineMarkSpawns.Count; i++)
        {
            if ((baselineMarkSpawns[i].position - candidate).sqrMagnitude < minSpacingSqr)
                return false;
        }

        return true;
    }

    private bool HasPlacementBlockerBaseline(Vector2 start, Vector2 end, float radius, int layerMask)
    {
        if (layerMask == 0)
            return false;

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        Vector2 direction = delta / distance;
        if (placementBenchmarkCounters != null) placementBenchmarkCounters.pathQueries++;
        if (radius <= 0f)
            return Physics2D.Linecast(start, end, layerMask).collider != null;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(start, radius, direction, distance, layerMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D collider = hits[i].collider;
            if (collider == null)
                continue;

            if (LightningSpearPlacementWorldQuery.IsInitialPlacementOverlapMovingAway(start, direction, collider, hits[i]))
                continue;

            return true;
        }

        return false;
    }
}
#endif
