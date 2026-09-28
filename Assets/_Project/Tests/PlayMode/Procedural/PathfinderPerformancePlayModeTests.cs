#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

/// <summary>Owns deterministic navigation fixtures, safety regressions and warmed per-query benchmark observations.</summary>
public sealed class PathfinderPerformancePlayModeTests
{
    private readonly List<Object> created = new();
    private TilemapPathfinder2D finder;
    private readonly MonsterNavigationFootprint2D small = new(Vector2.one * 0.3f, Vector2.zero);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    private GameObject Create(string name)
    {
        var go = new GameObject(name);
        created.Add(go);
        return go;
    }

    private void BuildFloor(int width = 32, int height = 24)
    {
        var grid = Create("BenchmarkGrid").AddComponent<Grid>();
        var floorObject = Create("BenchmarkFloor");
        floorObject.transform.SetParent(grid.transform);
        var floor = floorObject.AddComponent<Tilemap>();
        var tile = ScriptableObject.CreateInstance<Tile>();
        created.Add(tile);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++) floor.SetTile(new Vector3Int(x, y), tile);
        finder = Create("BenchmarkFinder").AddComponent<TilemapPathfinder2D>();
        Set("grid", grid);
        Set("groundTilemap", floor);
    }

    private BoxCollider2D Block(Vector2 center, Vector2 size, bool hole = false)
    {
        var collider = Create(hole ? "BenchmarkHole" : "BenchmarkWall").AddComponent<BoxCollider2D>();
        collider.gameObject.layer = hole ? 6 : 30;
        collider.isTrigger = hole;
        collider.transform.position = center;
        collider.size = size;
        return collider;
    }

    private void Set(string name, object value) => typeof(TilemapPathfinder2D).GetField(name, Private).SetValue(finder, value);

    [TestCase("open")]
    [TestCase("detour")]
    [TestCase("holes")]
    [TestCase("unreachable")]
    [TestCase("blocked-goal")]
    public void MeasureIdenticalQueries(string scenario)
    {
        BuildFloor();
        if (scenario == "detour")
        {
            Block(new Vector2(8, 9), new Vector2(0.15f, 18));
            Block(new Vector2(17, 15), new Vector2(0.15f, 18));
        }
        if (scenario == "holes") Block(new Vector2(15, 10), new Vector2(20, 2), true);
        if (scenario == "unreachable") Block(new Vector2(16, 12), new Vector2(0.15f, 30));
        if (scenario == "blocked-goal") Block(new Vector2(29.5f, 20.5f), Vector2.one);
        Physics2D.SyncTransforms();
        Vector2 start = new(1.5f, 1.5f), end = new(29.5f, 20.5f);
        for (int i = 0; i < 8; i++) finder.TryBuildPath(start, end, out _, small);
        bool success = finder.TryBuildPath(start, end, out var path, small);
        Assert.AreEqual(scenario != "unreachable", success);
        var expected = new List<Vector2>(path);
        int hash = 17;
        unchecked
        {
            foreach (var point in expected)
                hash = (hash * 31 + Mathf.RoundToInt(point.x * 2)) * 31 + Mathf.RoundToInt(point.y * 2);
        }
        // Captured from the unoptimized implementation on these exact fixtures.
        int baselineHash = scenario switch
        {
            "detour" => 377878403,
            "unreachable" => 17,
            "blocked-goal" => -1523660493,
            _ => 791461561
        };
        Assert.AreEqual(baselineHash, hash, "Preserve baseline waypoint order, including tie breaking.");
        // Keep fixture creation, assertions and reporting outside the timed/allocated region.
        const int samples = 21, repetitions = 5;
        var milliseconds = new double[samples];
        var bytes = new long[samples];
        long calibrationStart = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(new byte[4096]);
        bool allocationCounterWorks = GC.GetAllocatedBytesForCurrentThread() - calibrationStart >= 4096;
        for (int sample = 0; sample < samples; sample++)
        {
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            long beforeTime = Stopwatch.GetTimestamp();
            for (int i = 0; i < repetitions; i++) finder.TryBuildPath(start, end, out _, small);
            milliseconds[sample] = (Stopwatch.GetTimestamp() - beforeTime) * 1000.0 / Stopwatch.Frequency / repetitions;
            bytes[sample] = allocationCounterWorks
                ? (GC.GetAllocatedBytesForCurrentThread() - beforeBytes) / repetitions : -1;
            CollectionAssert.AreEqual(expected, path);
        }
        Array.Sort(milliseconds);
        Array.Sort(bytes);
        TestContext.WriteLine(FormattableString.Invariant(
            $"NAVBENCH,{scenario},median_ms={milliseconds[10]:F6},p95_ms={milliseconds[19]:F6},bytes={bytes[10]},visited={finder.DiagnosticVisitedNodes},walkability={finder.DiagnosticWalkabilityProbes},segments={finder.DiagnosticSegmentProbes},heuristics={finder.DiagnosticHeuristicEvaluations},success={success},points={expected.Count},hash={hash}"));
        if (success)
            for (int i = 1; i < expected.Count; i++)
                Assert.IsTrue(finder.HasDirectWalkableSegment(expected[i - 1], expected[i], small));
    }

    [Test]
    public void ThinWallBetweenClearCentersStillBlocksPath()
    {
        BuildFloor(4, 3);
        Block(new Vector2(2, 1.5f), new Vector2(0.05f, 5));
        Physics2D.SyncTransforms();
        Assert.IsFalse(finder.TryBuildPath(new Vector2(0.5f, 1.5f), new Vector2(3.5f, 1.5f), out _, small));
    }

    [Test]
    public void HoleTriggerStillBlocksPath()
    {
        BuildFloor(4, 3);
        Block(new Vector2(2, 1.5f), new Vector2(0.2f, 5), true);
        Physics2D.SyncTransforms();
        Assert.IsFalse(finder.TryBuildPath(new Vector2(0.5f, 1.5f), new Vector2(3.5f, 1.5f), out _, small));
    }

    [Test]
    public void NewQueryObservesChangedDoorGeometry()
    {
        BuildFloor(8, 3);
        var door = Block(new Vector2(4, 1.5f), new Vector2(0.2f, 5));
        door.enabled = false;
        Physics2D.SyncTransforms();
        Assert.IsTrue(finder.TryBuildPath(new Vector2(0.5f, 1.5f), new Vector2(7.5f, 1.5f), out _, small));
        door.enabled = true;
        Physics2D.SyncTransforms();
        Assert.IsFalse(finder.TryBuildPath(new Vector2(0.5f, 1.5f), new Vector2(7.5f, 1.5f), out _, small));
        door.enabled = false;
        Physics2D.SyncTransforms();
        Assert.IsTrue(finder.TryBuildPath(new Vector2(0.5f, 1.5f), new Vector2(7.5f, 1.5f), out _, small));
    }

    [Test]
    public void NewQueryDoesNotReuseDifferentBodySizeOrOffset()
    {
        BuildFloor(8, 1);
        Block(new Vector2(4, 1.1f), new Vector2(12, 0.4f));
        Block(new Vector2(4, -0.1f), new Vector2(12, 0.4f));
        Physics2D.SyncTransforms();
        Vector2 start = new(0.5f, 0.5f), end = new(7.5f, 0.5f);
        Assert.IsTrue(finder.TryBuildPath(start, end, out _, small));
        Assert.IsFalse(finder.TryBuildPath(start, end, out _, new MonsterNavigationFootprint2D(Vector2.one, Vector2.zero)));
        Assert.IsFalse(finder.TryBuildPath(start, end, out _, new MonsterNavigationFootprint2D(small.Size, Vector2.up * 0.4f)));
        Assert.IsTrue(finder.TryBuildPath(start, end, out _, small));
    }

    [Test]
    public void NewQueryObservesRemovedFloor()
    {
        BuildFloor(8, 3);
        Vector2 start = new(0.5f, 1.5f), end = new(7.5f, 1.5f);
        Assert.IsTrue(finder.TryBuildPath(start, end, out _, small));
        var floor = (Tilemap)typeof(TilemapPathfinder2D).GetField("groundTilemap", Private).GetValue(finder);
        floor.ClearAllTiles();
        Assert.IsFalse(finder.TryBuildPath(start, end, out _, small));
    }

    [Test]
    public void DiagonalNeighborsPreserveDirectRoute()
    {
        BuildFloor(4, 4);
        Set("allowDiagonal", true);
        Assert.IsTrue(finder.TryBuildPath(new Vector2(0.5f, 0.5f), new Vector2(3.5f, 3.5f), out var path, small));
        CollectionAssert.AreEqual(new[] { new Vector2(0.5f, 0.5f), new Vector2(1.5f, 1.5f),
            new Vector2(2.5f, 2.5f), new Vector2(3.5f, 3.5f) }, path);
    }

    [Test]
    public void DiagonalCannotCutThroughBlockedCorner()
    {
        BuildFloor(2, 2);
        Set("allowDiagonal", true);
        Block(new Vector2(1.5f, 0.5f), Vector2.one);
        Block(new Vector2(0.5f, 1.5f), Vector2.one);
        Physics2D.SyncTransforms();
        Assert.IsFalse(finder.TryBuildPath(new Vector2(0.5f, 0.5f), new Vector2(1.5f, 1.5f), out _, small));
    }
}
#endif
