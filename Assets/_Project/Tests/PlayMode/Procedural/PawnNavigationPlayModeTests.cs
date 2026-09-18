#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Verifies Pawn path guidance, search throttling and crawl rhythm without changing combat state.</summary>
public sealed class PawnNavigationPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> created = new();
    private PawnOrbitContactIntent2D pawn;
    private TilemapPathfinder2D finder;
    private BoxCollider2D wall;
    private readonly MonsterNavigationFootprint2D footprint = new(Vector2.one * 0.3f, Vector2.zero);
    private readonly Vector2 target = new(5.5f, 0.5f);

    [SetUp]
    public void SetUp()
    {
        var grid = Create("Grid").AddComponent<Grid>();
        GameObject floorObject = Create("Floor");
        floorObject.transform.SetParent(grid.transform);
        var floor = floorObject.AddComponent<Tilemap>();
        var tile = ScriptableObject.CreateInstance<Tile>();
        created.Add(tile);
        for (int y = -3; y <= 3; y++)
        for (int x = -1; x <= 7; x++) floor.SetTile(new Vector3Int(x, y), tile);
        finder = Create("Finder").AddComponent<TilemapPathfinder2D>();
        Set(finder, "grid", grid);
        Set(finder, "groundTilemap", floor);
        wall = Create("Wall").AddComponent<BoxCollider2D>();
        wall.gameObject.layer = 30;
        wall.transform.position = new Vector3(2.5f, 0.5f);
        wall.size = new Vector2(0.8f, 3f);
        pawn = CreatePawn("Pawn");
        Physics2D.SyncTransforms();
        typeof(PawnOrbitContactIntent2D).GetField("lastPawnSearchFrame", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, -1);
    }

    private GameObject Create(string name)
    {
        var result = new GameObject(name);
        created.Add(result);
        return result;
    }

    private PawnOrbitContactIntent2D CreatePawn(string name)
    {
        var result = Create(name).AddComponent<PawnOrbitContactIntent2D>();
        result.transform.position = new Vector3(0.5f, 0.5f);
        result.ApplySpawnContext(new MonsterSpawnContext(result.transform.position, Quaternion.identity, null, finder));
        Set(result, "nextPathSearchTime", -1f);
        return result;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void BlockedLineBuildsClearDetourAndReusesIt()
    {
        Assert.IsFalse(finder.HasDirectWalkableSegment(pawn.transform.position, target, footprint));
        Vector2 direction = Guide(pawn);
        Assert.Greater(direction.sqrMagnitude, 0.9f);
        var path = (List<Vector2>)Get(pawn, "approachPath");
        Assert.IsTrue(path.Exists(p => Mathf.Abs(p.y - 0.5f) > 1.5f));
        for (int i = 1; i < path.Count; i++)
            Assert.IsTrue(finder.HasDirectWalkableSegment(path[i - 1], path[i], footprint));
        float deadline = (float)Get(pawn, "nextPathSearchTime");
        Guide(pawn);
        Assert.AreEqual(deadline, Get(pawn, "nextPathSearchTime"));
        wall.enabled = false;
        Physics2D.SyncTransforms();
        Assert.IsTrue(finder.HasDirectWalkableSegment(pawn.transform.position, target, footprint));
    }

    [Test]
    public void OnlyOnePawnSearchRunsInOneFrame()
    {
        Assert.Greater(Guide(pawn).sqrMagnitude, 0f);
        var second = CreatePawn("Second");
        Assert.AreEqual(Vector2.zero, Guide(second));
        Assert.IsEmpty((List<Vector2>)Get(second, "approachPath"));
    }

    [Test]
    public void BlockedCachedSegmentStopsUntilRetry()
    {
        Guide(pawn);
        wall.transform.position = new Vector3(1f, 0.5f);
        Physics2D.SyncTransforms();
        Assert.AreEqual(Vector2.zero, Guide(pawn));
        Assert.IsEmpty((List<Vector2>)Get(pawn, "approachPath"));
    }

    [Test]
    public void CrawlPulseAndRestAreIdenticalForGuidedAndDirectDirections()
    {
        Set(pawn, "crawlPhaseOffset", 0f);
        Set(pawn, "crawlEpoch", Time.timeAsDouble - 0.11);
        IntentMovementData direct = Crawl(Vector2.right);
        IntentMovementData guided = Crawl(Vector2.up);
        Assert.AreEqual(direct.SpeedScale, guided.SpeedScale, 0.0001f);
        Assert.Greater(guided.SpeedScale, 0f);
        Assert.AreEqual(Vector2.up, guided.Direction);
        Set(pawn, "crawlEpoch", Time.timeAsDouble - 0.3);
        Assert.AreEqual(Vector2.zero, Crawl(Vector2.up).Direction);
    }

    [Test]
    public void StopClearsPathWithoutRestartingCrawlClock()
    {
        Guide(pawn);
        object epoch = Get(pawn, "crawlEpoch");
        pawn.StopChase();
        Assert.IsEmpty((List<Vector2>)Get(pawn, "approachPath"));
        Assert.AreEqual(epoch, Get(pawn, "crawlEpoch"));
    }

    private Vector2 Guide(PawnOrbitContactIntent2D source) => (Vector2)typeof(PawnOrbitContactIntent2D)
        .GetMethod("ResolvePathDirection", Private).Invoke(source, new object[] { finder, target, footprint });
    private IntentMovementData Crawl(Vector2 direction) => (IntentMovementData)typeof(PawnOrbitContactIntent2D)
        .GetMethod("CreateCrawlIntent", Private).Invoke(pawn, new object[] { direction, 2f });
    private static object Get(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
}
#endif
